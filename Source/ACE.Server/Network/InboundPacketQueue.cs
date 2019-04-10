using ACE.Server.Managers;
using log4net;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading;

namespace ACE.Server.Network
{
    public class InboundPacketQueue
    {
        public class RawInboundPacket
        {
            public IPEndPoint Us { get; set; }
            public IPEndPoint Them { get; set; }
            public byte[] Packet { get; set; }
        }
        private static readonly ILog packetLog = LogManager.GetLogger(System.Reflection.Assembly.GetEntryAssembly(), "Packets");
        private bool ProcessInboundPacketQueue = true;
        private AutoResetEvent InboundPacketArrived = new AutoResetEvent(false);
        private ManualResetEvent InboundPacketQueueProcessorExited = new ManualResetEvent(false);
        private ConcurrentQueue<RawInboundPacket> UnprocessedInboundPackets = new ConcurrentQueue<RawInboundPacket>();
        private Thread InboundPacketQueueProcessor = null;

        public InboundPacketQueue()
        {
            InboundPacketQueueProcessor = new Thread(new ThreadStart(Consumer))
            {
                Name = "InboundPacketQueueManager"
            };
            InboundPacketQueueProcessor.Start();
        }
        public void Shutdown()
        {
            ProcessInboundPacketQueue = false;
            InboundPacketQueueProcessorExited.WaitOne();
        }
        public void Consumer()
        {
            while (ProcessInboundPacketQueue)
            {
                RawInboundPacket rip = null;
                while (UnprocessedInboundPackets.TryDequeue(out rip))
                {
                    // TO-DO: generate ban entries here based on packet rates of endPoint, IP Address, and IP Address Range
                    if (packetLog.IsDebugEnabled)
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine($"Received Packet (Len: {rip.Packet.Length}) [{rip.Them.Address}:{rip.Them.Port}=>{rip.Us.Address}:{rip.Us.Port}]");
                        sb.AppendLine(rip.Packet.BuildPacketString());
                        packetLog.Debug(sb.ToString());
                    }
                    ClientPacket packet = new ClientPacket(rip.Packet);
                    if (packet.IsValid)
                    {
                        WorldManager.ProcessPacket(packet, rip.Them, rip.Us);
                    }
                }
                InboundPacketArrived.WaitOne(1000);
            }
            InboundPacketQueueProcessorExited.Set();
        }
        public void Enqueue(RawInboundPacket rip)
        {
            UnprocessedInboundPackets.Enqueue(rip);
            InboundPacketArrived.Set();
        }
    }
}
