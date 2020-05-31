using ACE.Common;
using System.Collections.Generic;
using System.IO;

namespace ACE.Server.Network
{
    public abstract class Packet : INeedCleanup
    {
        public const int MaxPacketSize = 484;
        public PacketHeader Header { get; protected set; } = new PacketHeader();
        public MemoryStream Data { get; internal set; }
        public List<PacketFragment> Fragments { get; private set; } = new List<PacketFragment>();

        public void ReleaseResources()
        {
            Data?.Dispose();
            Data = null;
            for (int i = 0; i < Fragments.Count; i++)
            {
                Fragments[i] = null;
            }
            Fragments = null;
        }
    }
}
