using ACE.Common;
using ACE.Common.Connection;
using ACE.Database;
using ACE.Database.Models.Auth;
using ACE.Entity.Enum;
using ACE.Server.Entity;
using ACE.Server.Managers;
using ACE.Server.Network.Enum;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Handlers;
using ACE.Server.Network.Packets;

using log4net;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace ACE.Server.Network.Managers
{
    public static class NetworkManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly ILog packetLog = LogManager.GetLogger(System.Reflection.Assembly.GetEntryAssembly(), "Packets");

        /// <summary>
        /// Seconds until a session will timeout. 
        /// Raising this value allows connections to remain active for a longer period of time. 
        /// </summary>
        /// <remarks>
        /// If you're experiencing network dropouts or frequent disconnects, try increasing this value.
        /// </remarks>
        public static uint DefaultSessionTimeout = ConfigManager.Config.Server.Network.DefaultSessionTimeout;

        public const ushort ServerId = 0xB;
        private static Random ClientIdWheel { get; set; } = new Random();
        private static ConcurrentDictionary<ulong, Session> Sessions { get; set; } = new ConcurrentDictionary<ulong, Session>();
        private static SocketManager SocketManager;
        private static NetQueue<ArrayPoolNetBuffer> OutboundQueue;

        public static void Initialize()
        {
            IPAddress host;
            try
            {
                host = IPAddress.Parse(ConfigManager.Config.Server.Network.Host);
            }
            catch (Exception)
            {
                host = IPAddress.Any;
            }
            SocketManager = new SocketManager();
            SocketManager.Initialize();
            SocketManager.Listen("Inbound Net Listener", true, InboundQueue_OnNextPacket);
            OutboundQueue = new NetQueue<ArrayPoolNetBuffer>("Outbound Net Queue", OutboundQueue_Next);
        }

        public static int GetSessionCount()
        {
            return Sessions.Count;
        }

        public static int GetUniqueSessionEndpointCount()
        {
            return Sessions
                .Select(k => Extensions.SessionIdToEndPoint(k.Key))
                .GroupBy(k => k.Address) // may need to impl EqualityComparer for Address.Equals(address)
                .Select(k => k.First())
                .Count();
        }

        public static int GetSessionEndpointTotalByAddressCount(IPAddress address)
        {
            return Sessions
                .Select(k => Extensions.SessionIdToEndPoint(k.Key).Address)
                .Where(k => k.Equals(address))
                .Count();
        }

        public static void Shutdown()
        {
            OutboundQueue.Shutdown();
            SocketManager.Stop();
        }

        public static int DoSessionWork()
        {
            int sessionCount = 0;
            // The session tick outbound processes pending actions and handles outgoing messages
            ServerPerformanceMonitor.RestartEvent(ServerPerformanceMonitor.MonitorType.DoSessionWork_TickOutbound);
            foreach (KeyValuePair<ulong, Session> s in Sessions)
            {
                s.Value.TickOutbound();
            }
            ServerPerformanceMonitor.RegisterEventEnd(ServerPerformanceMonitor.MonitorType.DoSessionWork_TickOutbound);

            // Removes sessions in the NetworkTimeout state, including sessions that have reached a timeout limit.
            ServerPerformanceMonitor.RestartEvent(ServerPerformanceMonitor.MonitorType.DoSessionWork_RemoveSessions);
            foreach (Session session in Sessions.Values)
            {
                if (session.PendingTermination != null)
                {
                    if (session.PendingTermination.TerminationStatus == SessionTerminationPhase.SessionWorkCompleted)
                    {
                        session.DropSession();
                        session.PendingTermination.WorldManagerWorkCompletedAt = DateTime.Now;
                        session.PendingTermination.TerminationStatus = SessionTerminationPhase.WorldManagerWorkCompleted;
                    }
                    else if (session.PendingTermination.TerminationStatus == SessionTerminationPhase.WorldManagerWorkCompleted)
                    {
                        session.PendingTermination.DoFinalTermination();
                    }
                }
                sessionCount++;
            }
            ServerPerformanceMonitor.RegisterEventEnd(ServerPerformanceMonitor.MonitorType.DoSessionWork_RemoveSessions);
            return sessionCount;
        }
        public static Session Find(uint accountId)
        {
            try
            {
                return Sessions.Values.FirstOrDefault(k => k != null && k.AccountId == accountId);
            }
            catch (Exception ex)
            {
                log.Warn(ex);
                return null;
            }
        }
        public static Session Find(string account)
        {
            try
            {
                return Sessions.Values.FirstOrDefault(k => k != null && k.Account == account);
            }
            catch (Exception ex)
            {
                log.Warn(ex);
                return null;
            }
        }
        public static void RemoveSession(Session session)
        {
            Sessions.Remove(session.Network.EndPoint.ToSessionId(), out Session xSession);
            xSession.ReleaseResources();
        }

        public static void SendPacket(ServerPacket packet, IPEndPoint EndPoint, SessionConnectionData ConnectionData = null)
        {
            if ((packet.Header.Flags & PacketHeaderFlags.EncryptedChecksum) != 0 && packet.IssacXor == 0)
            {
                if (ConnectionData == null)
                {
                    throw new ArgumentNullException("SendPacket was supplied a virgin encrypted checksum packet without session connection data.");
                }
                uint issacXor = ConnectionData.IssacServer.Next();
                packet.IssacXor = issacXor;
            }
            OutboundQueue.AddItem(new ArrayPoolNetBuffer(packet) { Peer = EndPoint });
        }
        private static void OutboundQueue_Next(ArrayPoolNetBuffer rp)
        {
            SocketManager.listener.Send(rp, true);
            NetworkStatistics.S2C_Packets_Aggregate_Increment();
        }
        private static void InboundQueue_OnNextPacket(ArrayPoolNetBuffer rPkt)
        {
            IPEndPoint remote = (IPEndPoint)rPkt.Peer;
            if (rPkt.Data.Length < 20)
            {
                return;
            }
            PacketHeaderFlags flags = rPkt.FlagPeek();
            if (flags == 0)
            {
                return;
            }
            ClientPacket packet = rPkt.ToClientPacket();
            if (!packet.SuccessfullyParsed)
            {
                return;
            }
            if (packet.Header.Flags == PacketHeaderFlags.LoginRequest)
            {
                PacketInboundLoginRequest loginRequest = new PacketInboundLoginRequest(packet);
                Account account = null;
                if (loginRequest.Account.Length > 50)
                {
                    return;
                }
                if (VerifyLoginRequest(loginRequest, ref account, remote.Address))
                {
                    if (account.BanExpireTime.HasValue)
                    {
                        var now = DateTime.UtcNow;
                        if (now < account.BanExpireTime.Value)
                        {
                            var reason = account.BanReason;
                            //TODO: write ServerPacket.AsByteArray so sessionless packets can be created
                            //session.Terminate(SessionTerminationReason.AccountBanned, new GameMessageBootAccount($"{(reason != null ? $" - {reason}" : null)}"), null, reason);
                            return;
                        }
                        else
                        {
                            account.UnBan();
                        }
                    }

                    List<Session> sessions = FindSessionByAccountOrEndPointAndEnsureSessionByEndpoint(account, remote);
                    foreach (Session session in sessions)
                    {
                        if (!session.EndPoint.Equals(remote) || session.State != SessionState.AuthLoginRequest)
                        {
                            if (session.State != SessionState.TerminationStarted)
                            {
                                // 2 second debounce: client spams login packets until it gets a ConnectRequest message and sometimes extra login packets arrive here that were sent beforehand
                                if (session.EndPoint.Equals(remote) && session.State == SessionState.AuthConnected && session.Age.Elapsed.TotalSeconds < 2)
                                {
                                    packetLog.Warn("debouncing login packet");
                                    return;
                                }
                                session.Terminate(SessionTerminationReason.AccountLoggedIn, new GameMessageBootAccount(SessionTerminationReasonHelper.GetDescription(SessionTerminationReason.AccountLoggedIn) + "\nfrom " + remote.ToString()));
                            }
                        }
                        else if (session.State == SessionState.AuthLoginRequest)
                        {
                            Login(session);
                        }
                    }
                }
                else
                {
                    log.Warn("!VerifyLoginRequest(loginRequest)");
                }
            }
            else
            {
                Session establishedSession = FindSession(remote, packet.Header.Id);
                if (establishedSession != null)
                {
                    establishedSession.Network.ProcessPacket(packet);
                }
                else
                {
                    packetLog.Debug("discarded packet");
                }
            }
        }
        private static void Login(Session session)
        {
            PacketOutboundConnectRequest connectRequest = new PacketOutboundConnectRequest(
                Timers.PortalYearTicks,
                session.Network.ConnectionData.ConnectionCookie,
                session.Network.ConnectionData.ClientId,
                session.Network.ConnectionData.ServerSeed,
                session.Network.ConnectionData.ClientSeed);
            session.State = SessionState.AuthConnected;
            session.Network.sendResync = true;
            session.Network.EnqueueSend(connectRequest);
            AuthenticationHandler.HandleConnectResponse(session);
        }
        private static bool VerifyLoginRequest(PacketInboundLoginRequest loginRequest, ref Account account, IPAddress remoteAddr)
        {
            if (loginRequest.NetAuthType != NetAuthType.AccountPassword || loginRequest.Password == "" || loginRequest.GlsTicket != null)
            {
                return false;
            }
            account = DatabaseManager.Authentication.GetAccountByName(loginRequest.Account);



            if (account == null)
            {
                if (ConfigManager.Config.Server.Accounts.AllowAutoAccountCreation)
                {
                    // no account, dynamically create one
                    if (WorldManager.WorldStatus == WorldManager.WorldStatusState.Open)
                    {
                        log.Info($"Auto creating account for: {loginRequest.Account}");
                    }
                    else
                    {
                        log.Debug($"Auto creating account for: {loginRequest.Account}");
                    }
                    AccessLevel accessLevel = (AccessLevel)ConfigManager.Config.Server.Accounts.DefaultAccessLevel;
                    if (!System.Enum.IsDefined(typeof(AccessLevel), accessLevel))
                    {
                        accessLevel = AccessLevel.Player;
                    }
                    account = DatabaseManager.Authentication.CreateAccount(loginRequest.Account.ToLower(), loginRequest.Password, accessLevel, remoteAddr);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else if (!account.PasswordMatches(loginRequest.Password))
            {
                return false;
            }
            else
            {
                return true;
            }
        }
        private static Session FindSession(IPEndPoint endPoint, ushort clientId)
        {
            if (!Sessions.TryGetValue(endPoint.ToSessionId(), out Session session))
            {
                return null;
            }
            else
            {
                return session.Network.ConnectionData.ClientId == clientId ? session : null;
            }
        }
        private static List<Session> FindSessionByAccountOrEndPointAndEnsureSessionByEndpoint(Account Account, IPEndPoint endPoint)
        {
            ulong sessionId = endPoint.ToSessionId();
            if (!Sessions.ContainsKey(sessionId))
            {
                ushort cid = NextClientId;
                log.Info(Session.FormatDetailedSessionId(endPoint, cid, Account.AccountName, null, "started"));
                Sessions.AddOrUpdate(sessionId, new Session(endPoint, Account, cid, ServerId), (a, b) => b);
            }
            return Sessions.Where(k => k.Key == sessionId || k.Value.AccountId == Account.AccountId).DefaultIfEmpty().Select(k => k.Value).ToList();
        }
        private static ushort NextClientId
        {
            get
            {
                IEnumerable<ushort> existingClientIds = Sessions.Values.Select(k => k.Network.ClientId);
                while (true)
                {
                    byte[] byaCid = new byte[2];
                    ClientIdWheel.NextBytes(byaCid);
                    ushort cid = BitConverter.ToUInt16(byaCid);
                    if (cid != 0 && !existingClientIds.Contains(cid))
                    {
                        return cid;
                    }
                }
            }
        }
        public static void DisconnectAllSessionsForShutdown()
        {
            Sessions.Values.ToList().ForEach(k =>
            {
                try
                {
                    k?.Terminate(SessionTerminationReason.ServerShuttingDown, new GameMessages.Messages.GameMessageCharacterError(CharacterError.ServerCrash1));
                }
                catch (Exception ex)
                {
                    log.Warn(ex);
                }
            });
        }
    }
}
