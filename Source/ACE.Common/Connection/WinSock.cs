using System;
using System.Net.Sockets;

namespace ACE.Common.Connection
{
    internal static class WinSock
    {
        /// <summary>
        /// From winsock2.h
        /// https://docs.microsoft.com/en-us/windows/win32/winsock/winsock-ioctls
        /// Winsock Socket input/output controls (IOCTLs)
        /// </summary>
        [Flags]
        internal enum IOC_Direction : uint
        {
            IOCPARM_MASK = 0x7f,
            IOC_VOID = 0x20000000,
            IOC_OUT = 0x40000000,
            IOC_IN = 0x80000000,
            IOC_INOUT = IOC_IN | IOC_OUT
        }
        /// <summary>
        /// From winsock2.h
        /// https://docs.microsoft.com/en-us/windows/win32/winsock/winsock-ioctls
        /// Winsock Socket input/output controls (IOCTLs)
        /// </summary>
        [Flags]
        internal enum IOC_Parameter : uint
        {
            IOC_UNIX = 0x00000000,
            IOC_WS2 = 0x08000000,
            IOC_PROTOCOL = 0x10000000,
            IOC_VENDOR = 0x18000000,
            SIO_UDP_CONNRESET = IOC_Direction.IOC_IN | IOC_VENDOR | 12
        }

        /// <summary>
        /// From winsock2.h
        /// https://docs.microsoft.com/en-us/windows/win32/winsock/winsock-ioctls
        /// Winsock Socket input/output controls (IOCTLs)
        /// </summary>
        private const int SIO_UDP_CONNRESET_INT = unchecked((int)IOC_Parameter.SIO_UDP_CONNRESET);//-1744830452;

        /// <summary>
        /// https://docs.microsoft.com/en-us/windows/win32/winsock/winsock-ioctls#sio_udp_connreset-opcode-setting-i-t3
        /// Enabled wrapper provides connection reset notifications in the form of a SocketException.
        /// Enable to throw an exception when trying to send UDP to a non-listening remote socket.
        /// Disable to ignore "The connection was reset by the remote peer." type errors.
        /// </summary>
        /// <param name="_enabled"></param>
        public static bool EnableConnectionResetWrapper(this Socket sock, bool _enabled)
        {
            byte[] inValue = new byte[] { _enabled ? (byte)1 : (byte)0 };
            byte[] outValue = new byte[] { 0 };
            try
            {
                int i = sock.IOControl(SIO_UDP_CONNRESET_INT, inValue, outValue);
                return i == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
