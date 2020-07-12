using ACE.Common;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ACE.Server.Network.Connection
{


    public abstract class NetBuffer : INeedCleanup
    {
        protected NetBuffer(bool AllocateDefaultBuffer)
        {
            if (AllocateDefaultBuffer)
            {
                Buffer = new byte[DEFAULT_BUFFER_SIZE];
            }
        }

        protected NetBuffer() { }
        public Socket WorkSocket { get; set; } = null;
        public byte[] Buffer { get; set; } = null;
        public int DataSize { get; set; } = 0;
        public const int DEFAULT_BUFFER_SIZE = 512;
        public EndPoint Peer { get; set; } = null;
        public bool Success { get; set; } = false;

        public void ReleaseResources()
        {
            WorkSocket = null;
            Buffer = null;
            Peer = null;
        }
    }
}
