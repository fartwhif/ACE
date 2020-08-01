using ACE.Common;
using ACE.Common.Connection;

using log4net;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ACE.Server.Network.Managers
{
    public class SocketManager
    {
        public bool IsListening => _IsListening();
        public Socket Socket => listener.sock;
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private CancellationTokenSource ListenerCanceller;
        internal ArrayPoolConnectionProvider listener;
        private IPEndPoint endp = null;
        private string strEndp = string.Empty;
        private Thread ListenThread = null;

        public void Initialize()
        {
            IPAddress host = GetConfiguredHost();
            endp = new IPEndPoint(host, (int)ConfigManager.Config.Server.Network.Port);
            strEndp = $"{host}:{ConfigManager.Config.Server.Network.Port}";
            ListenerCanceller = new CancellationTokenSource();
            listener = new ArrayPoolConnectionProvider(endp);
            log.Info($"Bound to host: {strEndp}");
        }
        private IPAddress GetConfiguredHost()
        {
            var hosts = new List<IPAddress>();
            try
            {
                var splits = ConfigManager.Config.Server.Network.Host.Split(",");
                foreach (var split in splits)
                    hosts.Add(IPAddress.Parse(split));
            }
            catch (Exception ex)
            {
                log.Error($"Unable to use {ConfigManager.Config.Server.Network.Host} as host due to: {ex}");
                log.Error("Using IPAddress.Any as host instead.");
                hosts.Clear();
                hosts.Add(IPAddress.Any);
            }
            if (hosts.Count > 1)
            {
                log.Warn("Multiple hosts configured. Multiple hosts are not supported. Using first host.");
            }
            if (hosts.Count < 1)
            {
                string err = "A host must be configured.  See setup documentation.";
                log.Fatal(err);
                throw new Exception(err);
            }
            return hosts[0];
        }
        private bool _IsListening()
        {
            if (listener == null)
            {
                throw new Exception("not initialized");
            }
            return listener.IsListening;
        }
        public void Stop()
        {
            if (listener == null)
            {
                throw new Exception("not initialized");
            }
            if (!listener.IsListening)
            {
                throw new Exception("not listening");
            }
            ListenerCanceller.Cancel();
            listener.DoneSignal.WaitOne();
            log.Info($"Stopped listening");
        }
        public void Listen(string ListenThreadName, bool queue, NetQueue<ArrayPoolNetBuffer>.OutputHandler dequeuedHandler, Action<ArrayPoolNetBuffer> directHandler = null)
        {
            if (listener == null)
            {
                throw new Exception("not initialized");
            }
            if (listener.IsListening)
            {
                throw new Exception("already listening");
            }
            ListenThread = new Thread(new ParameterizedThreadStart(BlockingListenThread))
            {
                Name = ListenThreadName
            };
            ListenThread.Start(new object[] { ListenThreadName, queue, dequeuedHandler, directHandler });
        }
        private void BlockingListenThread(object state)
        {
            object[] st = (object[])state;

            string ListenThreadName = (string)st[0];
            bool queue = (bool)st[1];
            NetQueue<ArrayPoolNetBuffer>.OutputHandler dequeuedHandler = (NetQueue<ArrayPoolNetBuffer>.OutputHandler)st[2];
            Action<ArrayPoolNetBuffer> directHandler = (Action<ArrayPoolNetBuffer>)st[3];

            //blocking
            log.Info($"Listening for inbound traffic from {strEndp}");
            listener.Listen(ListenThreadName, true, ListenerCanceller, dequeuedHandler, directHandler);
            log.Info($"Stopped listening for inbound traffic from {strEndp}");
        }
    }
}
