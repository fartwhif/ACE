using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

using log4net;

using ACE.Common;
using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Entity.Enum;
using ACE.Server.WorldObjects;
using ACE.Server.Managers;
using ACE.Server.Network.Enum;
using ACE.Server.Network.GameEvent.Events;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.GameMessages;
using ACE.Server.Network.Managers;
using ACE.Database.Models.Auth;
using System.Diagnostics;

namespace ACE.Server.Network
{
    public class Session : INeedCleanup
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public IPEndPoint EndPoint { get; private set; }

        public NetworkSession Network { get; set; }

        public uint GameEventSequence { get; set; }

        public SessionState State { get; set; }

        public Stopwatch Age { get; private set; } = Stopwatch.StartNew();

        public uint AccountId { get; private set; }

        public string Account { get; private set; }

        public string LoggingIdentifier { get; private set; } = "Unverified";

        public AccessLevel AccessLevel { get; private set; }

        public List<Character> Characters { get; } = new List<Character>();

        public Player Player { get; private set; }


        public DateTime logOffRequestTime;

        public DateTime lastCharacterSelectPingReply;

        public SessionTerminationDetails PendingTermination { get; set; } = null;

        public string BootSessionReason { get; private set; }

        public bool DatWarnCell;
        public bool DatWarnPortal;
        public bool DatWarnLanguage;

        public Session(IPEndPoint endPoint, Account account, ushort clientId, ushort serverId)
        {
            SetAccount(account);
            State = SessionState.AuthLoginRequest;
            EndPoint = endPoint;
            Network = new NetworkSession(this, clientId, serverId, endPoint);
        }

        public void ReleaseResources()
        {
            EndPoint = null;
            Network?.ReleaseResources();
            PendingTermination?.ReleaseResources();
            BootSessionReason = null;
            //TODO: clean for characters initiated here
            //foreach (Character q in Characters)
            //{
            //    q.Dispose();
            //}
            Characters.Clear();
            //Player?.Dispose();//TODO: clean for player initiated here
        }

        private bool CheckState(ClientPacket packet)
        {
            if (packet.Header.HasFlag(PacketHeaderFlags.LoginRequest) && State != SessionState.AuthLoginRequest)
            {
                return false;
            }
            if (packet.Header.HasFlag(PacketHeaderFlags.ConnectResponse) && State != SessionState.AuthConnectResponse)
            {
                return false;
            }
            if (packet.Header.HasFlag(PacketHeaderFlags.AckSequence | PacketHeaderFlags.TimeSync | PacketHeaderFlags.EchoRequest | PacketHeaderFlags.Flow) && State == SessionState.AuthLoginRequest)
            {
                return false;
            }
            return true;
        }


        public void ProcessPacket(ClientPacket packet)
        {
            if (!CheckState(packet))
                return;

            Network.ProcessPacket(packet);
        }

        public void TickOutbound()
        {
            if (Network == null)
            {
                return;
            }
            // Check if the player has been booted
            if (PendingTermination != null)
            {
                if (PendingTermination.TerminationStatus == SessionTerminationPhase.Initialized)
                {
                    State = SessionState.TerminationStarted;
                    Network.Update(); // boot messages may need sending
                    if (DateTime.UtcNow.Ticks > PendingTermination.TerminationEndTicks)
                    {
                        PendingTermination.TerminationStatus = SessionTerminationPhase.SessionWorkCompleted;
                    }
                }
                return;
            }
            if (State == SessionState.TerminationStarted)
            {
                return;
            }
            // Checks if the session has stopped responding.
            if (DateTime.UtcNow.Ticks >= Network.TimeoutTick)
            {
                // The Session has reached a timeout.  Send the client the error disconnect signal, and then drop the session
                Terminate(SessionTerminationReason.NetworkTimeout);
                return;
            }
            Network.Update();
            // Live server seemed to take about 6 seconds. 4 seconds is nice because it has smooth animation, and saves the user 2 seconds every logoff
            // This could be made 0 for instant logoffs.
            if (logOffRequestTime != DateTime.MinValue && logOffRequestTime.AddSeconds(6) <= DateTime.UtcNow)
            {
                SendFinalLogOffMessages();
            }

            // This section deviates from known retail pcaps/behavior, but appears to be the least harmful way to work around something that seemingly didn't occur to players using ThwargLauncher connecting to retail servers.
            // In order to prevent the launcher from thinking the session is dead, we will send a Ping Response every 100 seconds, this will in effect make the client appear active to the launcher and allow players to create characters in peace.
            if (State == SessionState.AuthConnected) // TODO: why is this needed? Why didn't retail have this problem? Is this fuzzy memory?
            {
                if (lastCharacterSelectPingReply == DateTime.MinValue)
                    lastCharacterSelectPingReply = DateTime.UtcNow.AddSeconds(100);
                else if (DateTime.UtcNow > lastCharacterSelectPingReply)
                {
                    Network.EnqueueSend(new GameEventPingResponse(this));
                    lastCharacterSelectPingReply = DateTime.UtcNow.AddSeconds(100);
                }
            }
            else if (lastCharacterSelectPingReply != DateTime.MinValue)
                lastCharacterSelectPingReply = DateTime.MinValue;
        }

        public void SetAccount(Account account)
        {
            AccountId = account.AccountId;
            Account = account.AccountName;
            AccessLevel = (AccessLevel)account.AccessLevel;
        }

        public void UpdateCharacters(IEnumerable<Character> characters)
        {
            Characters.Clear();

            Characters.AddRange(characters);

            CheckCharactersForDeletion();
        }

        public void CheckCharactersForDeletion()
        {
            for (int i = Characters.Count - 1; i >= 0; i--)
            {
                if (Characters[i].DeleteTime > 0 && Time.GetUnixTime() > Characters[i].DeleteTime)
                {
                    Characters[i].IsDeleted = true;

                    DatabaseManager.Shard.SaveCharacter(Characters[i], new ReaderWriterLockSlim(), null);

                    PlayerManager.ProcessDeletedPlayer(Characters[i].Id);

                    Characters.RemoveAt(i);
                }
            }
        }

        public void InitSessionForWorldLogin()
        {
            GameEventSequence = 1;
        }

        public void SetAccessLevel(AccessLevel accountAccesslevel)
        {
            AccessLevel = accountAccesslevel;
        }

        public void SetPlayer(Player player)
        {
            Player = player;
        }


        /// <summary>
        /// Log off the player normally
        /// </summary>
        public void LogOffPlayer(bool forceImmediate = false)
        {
            if (Player == null) return;

            if (logOffRequestTime == DateTime.MinValue)
            {
                var result = Player.LogOut(false, forceImmediate);

                if (result)
                    logOffRequestTime = DateTime.UtcNow;
            }
        }

        private void SendFinalLogOffMessages()
        {
            // If we still exist on a landblock, we can't exit yet.
            if (Player.CurrentLandblock != null)
            {
                return;
            }

            logOffRequestTime = DateTime.MinValue;

            // It's possible for a character change to happen from a GameActionSetCharacterOptions message.
            // This message can be received/processed by the server AFTER LogOfPlayer has been called.
            // What that means is, we could end up with Character changes after the Character has been saved from the initial LogOff request.
            // To make sure we commit these additional changes (if any), we check again here
            if (Player.CharacterChangesDetected)
            {
                Player.SaveCharacterToDatabase();
            }

            Player = null;

            if (!ServerManager.ShutdownInProgress)
            {
                Network.EnqueueSend(new GameMessageCharacterLogOff());

                CheckCharactersForDeletion();

                Network.EnqueueSend(new GameMessageCharacterList(Characters, this));

                GameMessageServerName serverNameMessage = new GameMessageServerName(ConfigManager.Config.Server.WorldName, PlayerManager.GetOnlineCount(), (int)ConfigManager.Config.Server.Network.MaximumAllowedSessions);
                Network.EnqueueSend(serverNameMessage);
            }

            State = SessionState.AuthConnected;
        }
        public void Terminate(SessionTerminationReason reason, GameMessage message = null, ServerPacket packet = null, string extraReason = "", Action TerminationCompleted = null)
        {
            if (PendingTermination != null)
            {
                return;
            }
            // TODO: graceful SessionTerminationReason.AccountBooted handling
            if (packet != null)
            {
                Network.EnqueueSend(packet);
            }
            if (message != null)
            {
                Network.EnqueueSend(message);
            }
            Action tc = (TerminationCompleted == null) ? new Action(() => { NetworkManager.RemoveSession(this); }) : TerminationCompleted;
            PendingTermination = new SessionTerminationDetails()
            {
                ExtraReason = extraReason,
                Reason = reason,
                FinalTerminationAction = tc
            };
        }
        public void DropSession()
        {
            if (PendingTermination == null || PendingTermination.TerminationStatus != SessionTerminationPhase.SessionWorkCompleted)
            {
                return;
            }
            if (PendingTermination.Reason != SessionTerminationReason.PongSentClosingConnection)
            {
                string msg = FormatDetailedSessionId(EndPoint, Network?.ClientId ?? 0, Account, Player, "dropped", PendingTermination.Reason, PendingTermination.ExtraReason);
                if (WorldManager.WorldStatus == WorldManager.WorldStatusState.Open)
                {
                    log.Info(msg);
                }
                else
                {
                    log.Debug(msg);
                }
            }
            if (Player != null)
            {
                PendingTermination.FinalTerminationDelay = TimeSpan.FromSeconds(7);
                LogOffPlayer();
                // We don't want to set the player to null here. Because the player is still on the network, it may still enqueue work onto it's session.
                // Some network message objects will reference session.Player in their construction. If we set Player to null here, we'll throw exceptions in those cases.
                // At this point, if the player was on a landblock, they'll still exist on that landblock until the logout animation completes (~6s).
            }
        }

        public static string FormatDetailedSessionId(IPEndPoint EndPoint, ushort ClientId, string AccountName, Player Player = null, string verb = null, SessionTerminationReason reason = SessionTerminationReason.None, string extraReason = null)
        {
            string reas = (reason != SessionTerminationReason.None) ? $", Reason: {reason.GetDescription()}" : "";
            if (!string.IsNullOrWhiteSpace(extraReason))
            {
                reas = reas + ", " + extraReason;
            }
            string ver = (verb != null) ? $" {verb}. " : ", ";
            string plr = (Player != null) ? $", Player: {Player.Name}" : "";
            return $"Session {ClientId}\\{EndPoint}{ver}Account: {AccountName}{plr}{reas}";
        }

        public void SendCharacterError(CharacterError error)
        {
            Network.EnqueueSend(new GameMessageCharacterError(error));
        }

        /// <summary>
        /// Sends a broadcast message to the player
        /// </summary>
        public void WorldBroadcast(string broadcastMessage)
        {
            GameMessageSystemChat worldBroadcastMessage = new GameMessageSystemChat(broadcastMessage, ChatMessageType.WorldBroadcast);
            Network.EnqueueSend(worldBroadcastMessage);
        }


    }
}
