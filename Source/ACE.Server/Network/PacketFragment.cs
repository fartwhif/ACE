
using ACE.Common;

namespace ACE.Server.Network
{
    public abstract class PacketFragment: INeedCleanup
    {
        public static int MaxFragementSize { get; } = 464; // Packet.MaxPacketSize - PacketHeader.HeaderSize
        public static int MaxFragmentDataSize { get; } = 448; // Packet.MaxPacketSize - PacketHeader.HeaderSize - PacketFragmentHeader.HeaderSize

        public PacketFragmentHeader Header { get; protected set; } = new PacketFragmentHeader();
        public byte[] Data { get; protected set; }

        public int Length => PacketFragmentHeader.HeaderSize + Data?.Length ?? 0;

        public void ReleaseResources()
        {
            Data = null;
        }
    }
}
