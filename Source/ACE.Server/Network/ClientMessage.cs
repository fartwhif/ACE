using ACE.Common;
using System.IO;

namespace ACE.Server.Network
{
    public class ClientMessage : INeedCleanup
    {
        public BinaryReader Payload { get; private set; }

        public MemoryStream Data { get; private set; }

        public uint Opcode { get; }

        public ClientMessage(MemoryStream stream)
        {
            Data = stream;
            Payload = new BinaryReader(Data);
            Opcode = Payload.ReadUInt32();
        }

        public ClientMessage(byte[] data)
        {
            Data = new MemoryStream(data);
            Payload = new BinaryReader(Data);
            Opcode = Payload.ReadUInt32();
        }

        public void ReleaseResources()
        {
            Payload.Dispose();
            Data.Dispose();
            Payload = null;
            Data = null;
        }
    }
}
