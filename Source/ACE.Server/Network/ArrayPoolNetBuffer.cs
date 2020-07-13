using ACE.Server.Network;

using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace ACE.Common.Connection
{
    public class ArrayPoolNetBuffer : SimpleNetBuffer, INeedCleanup
    {
        public ArrayPoolNetBuffer() { Allocate(DEFAULT_BUFFER_SIZE); }
        public ArrayPoolNetBuffer(ServerPacket packet)
        {
            Buffer = ArrayPool<byte>.Shared.Rent((int)(PacketHeader.HeaderSize + (packet.Data?.Length ?? 0) + (packet.Fragments.Count * PacketFragment.MaxFragementSize)));
            packet.CreateReadyToSendPacket(Buffer, out int size);
            Data = new ReadOnlyMemory<byte>(Buffer, 0, size);
            DataSize = size;
        }
        public ArrayPoolNetBuffer(int size)
        {
            Allocate(size);
        }
        private void Allocate(int size)
        {
            Buffer = ArrayPool<byte>.Shared.Rent(size);
            Data = new ReadOnlyMemory<byte>(Buffer, 0, size);
            DataSize = size;
        }
        public ReadOnlyMemory<byte> Data { get; set; }
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
            ArrayPool<byte>.Shared.Return(Buffer);
            base.ReleaseResources();
        }
    }
}
