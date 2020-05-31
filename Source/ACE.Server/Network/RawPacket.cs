using ACE.Common;

using System;
using System.Buffers;
using System.Net;
using System.Runtime.InteropServices;

namespace ACE.Server.Network
{
    public class RawPacket : INeedCleanup
    {
        public IPEndPoint Local { get; set; }
        public IPEndPoint Remote { get; set; }
        public ReadOnlyMemory<byte> Data { get; set; }
        public byte[] Lease { get; set; }
        public ClientPacket ToClientPacket()
        {
            return new ClientPacket(Data);
        }
        public PacketHeaderFlags FlagPeek()
        {
            if (Data.Length < 8)
            {
                return 0;
            }
            ReadOnlySpan<byte> pData = Data.Slice(4, 4).Span;
            ReadOnlySpan<PacketHeaderFlags> pFlags = MemoryMarshal.Cast<byte, PacketHeaderFlags>(pData);
            ref PacketHeaderFlags flags = ref MemoryMarshal.GetReference(pFlags);
            return flags;
        }
        public void ReleaseResources()
        {
            Local = null;
            Remote = null;
            Data = null;
            ArrayPool<byte>.Shared.Return(Lease);
            Lease = null;
        }
    }
}
