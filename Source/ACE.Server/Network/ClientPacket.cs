
using System;
using System.IO;

using log4net;

using ACE.Common.Cryptography;
using ACE.Common;

namespace ACE.Server.Network
{
    public class ClientPacket : Packet, INeedCleanup
    {
        private static readonly ILog packetLog = LogManager.GetLogger(System.Reflection.Assembly.GetEntryAssembly(), "Packets");
        public BinaryReader DataReader { get; private set; }
        public PacketHeaderOptional HeaderOptional { get; private set; }
        public bool SuccessfullyParsed { get; private set; } = false;
        public bool? ValidCRC { get; private set; } = null;
        public ClientPacket(ReadOnlyMemory<byte> data)
        {
            //data = NetworkSyntheticTesting.SyntheticCorruption_C2S(data);
            ParsePacketData(data);
            if (SuccessfullyParsed)
            {
                ReadFragments();
            }
        }

        private unsafe void ParsePacketData(ReadOnlyMemory<byte> data)
        {
            fixed (byte* pBuffer = &data.Span[0])
            {
                try
                {
                    using (UnmanagedMemoryStream stream = new UnmanagedMemoryStream(pBuffer, data.Length))
                    {
                        //TO-DO: use new pattern instead
                        using (BinaryReader reader = new BinaryReader(stream))
                        {
                            Header = new PacketHeader(reader);
                            if (Header.Size > data.Length - reader.BaseStream.Position)
                            {
                                SuccessfullyParsed = false;
                                return;
                            }
                            Data = new MemoryStream(reader.ReadBytes(Header.Size), 0, Header.Size, false, true);
                            DataReader = new BinaryReader(Data);
                            HeaderOptional = new PacketHeaderOptional(DataReader, Header);
                            if (!HeaderOptional.IsValid)
                            {
                                SuccessfullyParsed = false;
                                return;
                            }
                        }
                    }
                    SuccessfullyParsed = true;
                }
                catch (Exception ex)
                {
                    SuccessfullyParsed = false;
                    //packetLogger.Error(this, "Invalid packet data", ex);
                }
            }
        }
        private void ReadFragments()
        {
            if (Header.HasFlag(PacketHeaderFlags.BlobFragments))
            {
                while (DataReader.BaseStream.Position != DataReader.BaseStream.Length)
                {
                    try
                    {
                        Fragments.Add(new ClientPacketFragment(DataReader));
                    }
                    catch (Exception)
                    {
                        // corrupt packet
                        SuccessfullyParsed = false;
                        break;
                    }
                }
            }
        }

        private uint? _fragmentChecksum;
        private uint fragmentChecksum
        {
            get
            {
                if (_fragmentChecksum == null)
                {
                    uint fragmentChecksum = 0u;

                    foreach (ClientPacketFragment fragment in Fragments)
                        fragmentChecksum += fragment.CalculateHash32();

                    _fragmentChecksum = fragmentChecksum;
                }

                return _fragmentChecksum.Value;
            }
        }

        private uint? _headerChecksum;
        private uint headerChecksum
        {
            get
            {
                if (_headerChecksum == null)
                    _headerChecksum = Header.CalculateHash32();

                return _headerChecksum.Value;
            }
        }

        private uint? _headerOptionalChecksum;
        private uint headerOptionalChecksum
        {
            get
            {
                if (_headerOptionalChecksum == null)
                    _headerOptionalChecksum = HeaderOptional.CalculateHash32();

                return _headerOptionalChecksum.Value;
            }
        }

        private uint? _payloadChecksum;
        private uint payloadChecksum
        {
            get
            {
                if (_payloadChecksum == null)
                    _payloadChecksum = headerOptionalChecksum + fragmentChecksum;

                return _payloadChecksum.Value;
            }
        }

        public bool VerifyCRC(CryptoSystem fq)
        {
            if (Header.HasFlag(PacketHeaderFlags.EncryptedChecksum))
            {
                var key = ((Header.Checksum - headerChecksum) ^ payloadChecksum);
                if (fq.Search(key))
                {
                    fq.ConsumeKey(key);
                    return true;
                }
            }
            else
            {
                if (headerChecksum + payloadChecksum == Header.Checksum)
                {
                    packetLog.DebugFormat("{0}", this);
                    return true;
                }

                packetLog.DebugFormat("{0}, Checksum Failed", this);
            }

            NetworkStatistics.C2S_CRCErrors_Aggregate_Increment();

            return false;
        }

        public override string ToString()
        {
            return $"<<< {Header} {HeaderOptional}".TrimEnd();
        }
    }
}
