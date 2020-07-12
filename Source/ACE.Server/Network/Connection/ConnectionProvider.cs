using ACE.Common;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ACE.Server.Network.Connection
{
    public class ConnectionProvider<T> : INeedCleanup where T : INeedCleanup
    {
        public ManualResetEvent DoneSignal { get; private set; } = default;
        public Queue<T> Arrived { get; set; } = null;
        public Action<T> _ArrivedCallback = null;

        internal Socket sock = default;
        internal IPEndPoint localListenIPEndPoint = default;
        internal EndPoint LocalSocket = default;
        internal long ZeroDoneSignal = -1;
        internal CancellationTokenSource _CancelSignal = default;

        private bool _WithQueue = false;

        public virtual void Listen(string ListenThreadName, bool WithQueue, CancellationTokenSource CancelSignal, Queue<T>.OutputHandler handler, Action<T> ArrivedCallback = null)
        {
            _WithQueue = WithQueue;
            _ArrivedCallback = ArrivedCallback;
            _CancelSignal = CancelSignal;
            if (WithQueue)
            {
                Arrived = new Queue<T>(ListenThreadName + " InQueue", handler);
            }
        }
        public void ListenCancel()
        {
            _CancelSignal.Cancel();
            DoneSignal?.WaitOne();
            if (_WithQueue)
            {
                Arrived.Shutdown();
                Arrived.ReleaseResources();
            }
        }

        public bool IsListening => _IsListening();
        internal bool _IsListening()
        {
            return Interlocked.Read(ref ZeroDoneSignal) == 0;
        }
        public ConnectionProvider(IPEndPoint listenPoint)
        {
            DoneSignal = new ManualResetEvent(false);
            localListenIPEndPoint = listenPoint;
            LocalSocket = localListenIPEndPoint;
            sock = new Socket(LocalSocket.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            // reserve listening socket with kernel
            sock.Bind(LocalSocket);
            sock.EnableConnectionResetWrapper(false);
        }

        public void ReleaseResources()
        {
            if (IsListening)
            {
                ListenCancel();
            }
            DoneSignal?.Close();
            DoneSignal?.Dispose();
            DoneSignal = null;
            Arrived?.Shutdown();
            Arrived?.ReleaseResources();
            Arrived = null;

            try
            {
                sock?.Close();
            }
            finally
            {
                try
                {
                    sock?.Dispose();
                }
                finally
                {
                    sock = null;
                }
            }
        }
    }
}
