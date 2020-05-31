using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Sockets;
using ACE.Entity;
using ACE.Server.Network.Enum;

namespace ACE.Server.Network
{
    public static class Extensions
    {
        public static ulong ToSessionId(this IPEndPoint endPoint)
        {
            if (endPoint.AddressFamily != AddressFamily.InterNetwork)
            {
                return 0;
            }
            byte[] buffer = ArrayPool<byte>.Shared.Rent(8);
            Span<byte> sessionIdBytes = new Span<byte>(buffer, 0, 8);
            Span<byte> hiWord = sessionIdBytes.Slice(0, 4);
            Span<byte> loWord = sessionIdBytes.Slice(4, 4);
            try
            {
                int bytesWritten = -1;
                bool success = endPoint.Address.TryWriteBytes(hiWord, out bytesWritten);
                if (!success || bytesWritten != 4)
                {
                    throw new Exception("could not convert ip address to bytes");
                }
                if (!BitConverter.TryWriteBytes(loWord, endPoint.Port))
                {
                    throw new Exception("could not convert port to bytes");
                }
                ulong sessionId = BitConverter.ToUInt64(sessionIdBytes);
                return sessionId;
            }
            finally
            {
                loWord = null;
                hiWord = null;
                sessionIdBytes = null;
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        public static IPEndPoint SessionIdToEndPoint(ulong sessionId)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(8);
            Span<byte> sessionIdBytes = new Span<byte>(buffer, 0, 8);
            Span<byte> hiWord = sessionIdBytes.Slice(0, 4);
            Span<byte> loWord = sessionIdBytes.Slice(4, 4);
            try
            {
                if (BitConverter.TryWriteBytes(sessionIdBytes, sessionId))
                {
                    return null;
                }
                int port = BitConverter.ToInt32(loWord);
                IPAddress ipaddr = new IPAddress(hiWord);
                IPEndPoint endp = new IPEndPoint(ipaddr, port);
                return endp;
            }
            finally
            {
                loWord = null;
                hiWord = null;
                sessionIdBytes = null;
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static uint CalculatePadMultiple(uint length, uint multiple) { return multiple * ((length + multiple - 1u) / multiple) - length; }

        public static void WriteString16L(this BinaryWriter writer, string data)
        {
            if (data == null) data = "";

            writer.Write((ushort)data.Length);
            writer.Write(System.Text.Encoding.GetEncoding(1252).GetBytes(data));

            // client expects string length to be a multiple of 4 including the 2 bytes for length
            writer.Pad(CalculatePadMultiple(sizeof(ushort) + (uint)data.Length, 4u));
        }

        public static void WritePackedDword(this BinaryWriter writer, uint value)
        {
            if (value <= 32767)
            {
                ushort networkValue = Convert.ToUInt16(value);
                writer.Write(BitConverter.GetBytes(networkValue));
            }
            else
            {
                uint packedValue = (value << 16) | ((value >> 16) | 0x8000);
                writer.Write(BitConverter.GetBytes(packedValue));
            }
        }

        public static void WritePackedDwordOfKnownType(this BinaryWriter writer, uint value, uint type)
        {
            if ((value & type) > 0)
                value -= type;

            writer.WritePackedDword(value);
        }

        public static void WriteUInt16BE(this BinaryWriter writer, ushort value)
        {
            ushort beValue = (ushort)((ushort)((value & 0xFF) << 8) | ((value >> 8) & 0xFF));
            writer.Write(beValue);
        }

        public static void Pad(this BinaryWriter writer, uint pad) { writer.Write(new byte[pad]); }

        public static void Pad(this BinaryReader reader, uint pad) { reader.ReadBytes((int)pad); }

        public static void Align(this BinaryWriter writer)
        {
            writer.Pad(CalculatePadMultiple((uint)writer.BaseStream.Length, 4u));
        }

        public static void Align(this BinaryReader reader)
        {
            reader.Pad(CalculatePadMultiple((uint)reader.BaseStream.Position, 4u));
        }

        public static string BuildPacketString(this byte[] bytes, int startPosition = 0, int bytesToOutput = 9999)
        {
            TextWriter tw = new StringWriter();
            byte[] buffer = bytes;

            int column = 0;
            int row = 0;
            int columns = 16;
            tw.Write("   x  ");
            for (int i = 0; i < columns; i++)
            {
                tw.Write(i.ToString().PadLeft(3));
            }
            tw.WriteLine("  |Text");
            tw.Write("   0  ");

            string asciiLine = "";
            for (int i = startPosition; i < startPosition+bytesToOutput; i++)
            {
                if (i >= buffer.Length) {
                    break;
                }
                if (column >= columns)
                {
                    row++;
                    column = 0;
                    tw.WriteLine("  |" + asciiLine);
                    asciiLine = "";
                    tw.Write((row * columns).ToString().PadLeft(4));
                    tw.Write("  ");
                }

                tw.Write(buffer[i].ToString("X2").PadLeft(3));

                if (Char.IsControl((char)buffer[i]))
                    asciiLine += " ";
                else
                    asciiLine += (char)buffer[i];
                column++;
            }

            tw.Write("".PadLeft((columns - column) * 3));
            tw.WriteLine("  |" + asciiLine);
            return tw.ToString();
        }

        public static void WritePosition(this BinaryWriter writer, uint value, long position)
        {
            long originalPosition = writer.BaseStream.Position;
            writer.BaseStream.Position = position;
            writer.Write(value);
            writer.BaseStream.Position = originalPosition;
        }

        public static ObjectGuid ReadGuid(this BinaryReader reader) { return new ObjectGuid(reader.ReadUInt32()); }

        public static void WriteGuid(this BinaryWriter writer, ObjectGuid guid) { writer.Write(guid.Full); }
    }
}
