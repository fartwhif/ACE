using ACE.Server.Network.Managers;

using log4net;

using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ACE.Server.Network
{
    public class ConnectionListener
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly ILog packetLog = LogManager.GetLogger(System.Reflection.Assembly.GetEntryAssembly(), "Packets");

        public Socket Socket { get; private set; }
        private IPEndPoint listenerEndpoint;
        private EndPoint anyRemoteEndpoint;
        private readonly uint listeningPort;
        private byte[] buffer = null;
        private readonly IPAddress listeningHost;
        private ManualResetEvent ReceiveComplete = new ManualResetEvent(false);

        public ConnectionListener(IPAddress host, uint port)
        {
            log.InfoFormat("ConnectionListener ctor, host {0} port {1}", host, port);
            listeningHost = host;
            listeningPort = port;
            anyRemoteEndpoint = new IPEndPoint(listeningHost, 0);
            try
            {
                log.InfoFormat("Binding ConnectionListener, host {0} port {1}", listeningHost, listeningPort);
                listenerEndpoint = new IPEndPoint(listeningHost, (int)listeningPort);
                Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                Socket.Bind(listenerEndpoint);
            }
            catch (Exception ex)
            {
                log.Fatal(ex);
            }
        }
        public void Start()
        {
            try
            {
                Listen();
            }
            catch (Exception ex)
            {
                log.Fatal(ex);
            }
        }
        public void Shutdown()
        {
          packetLog.InfoFormat( "Shutting down ConnectionListener, host {0} port {1}", listeningHost, listeningPort);
            if (Socket != null && Socket.IsBound)
            {
                Socket.Close();
            }
        }
        private void Listen()
        {
            buffer = ArrayPool<byte>.Shared.Rent(Packet.MaxPacketSize);
            while (true)
            {
                try
                {
                    ReceiveComplete.Reset();
                    Socket.BeginReceiveFrom(buffer, 0, Packet.MaxPacketSize, SocketFlags.None, ref anyRemoteEndpoint, OnDataReceieve, Socket);
                    ReceiveComplete.WaitOne();
                }
                catch (SocketException sex)
                {
                    if ((sex.SocketErrorCode == SocketError.ConnectionAborted) ||
                        (sex.SocketErrorCode == SocketError.ConnectionRefused) ||
                        (sex.SocketErrorCode == SocketError.ConnectionReset) ||
                        (sex.SocketErrorCode == SocketError.OperationAborted) &&
                        (sex.SocketErrorCode != SocketError.MessageSize) &&
                        (sex.SocketErrorCode != SocketError.NetworkReset))
                    {
                        continue;
                    }
                    log.Warn(sex);
                    break;
                }
                catch (Exception ex)
                {
                    log.Fatal(ex);
                    break;
                }
            }
        }
        private void OnDataReceieve(IAsyncResult result)
        {
            EndPoint remoteEndPoint = null;
            try
            {
                remoteEndPoint = new IPEndPoint(listeningHost, 0);
                int dataSize = Socket.EndReceiveFrom(result, ref remoteEndPoint);
                NetworkManager.EnqueueInbound(new RawPacket { Data = new ReadOnlyMemory<byte>(buffer, 0, dataSize), Remote = (IPEndPoint)remoteEndPoint, Local = listenerEndpoint, Lease = buffer });
                buffer = ArrayPool<byte>.Shared.Rent(Packet.MaxPacketSize);
            }
            catch (SocketException sex)
            {
                if ((sex.SocketErrorCode != SocketError.ConnectionAborted) &&
                       (sex.SocketErrorCode != SocketError.ConnectionRefused) &&
                       (sex.SocketErrorCode != SocketError.ConnectionReset) &&
                       (sex.SocketErrorCode != SocketError.OperationAborted) &&
                       (sex.SocketErrorCode != SocketError.MessageSize) &&
                       (sex.SocketErrorCode != SocketError.NetworkReset))
                {
                    packetLog.Warn(sex);
                }
            }
            catch (Exception ex)
            {
               packetLog.Warn(ex);
            }
            finally
            {
                ReceiveComplete.Set();
            }
        }
    }
}
