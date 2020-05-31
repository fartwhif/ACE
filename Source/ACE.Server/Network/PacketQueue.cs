using ACE.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ACE.Server.Network
{
    public class PacketQueue : INeedCleanup
    {
        private BlockingCollection<RawPacket> UnprocessedPackets = new BlockingCollection<RawPacket>();
        private Task _readerTask;
        private Thread InboundPacketQueueProcessor = null;
        public int QueueLength => UnprocessedPackets.Count;
        public delegate void RawPacketEventArgs(RawPacket rp);
        public event RawPacketEventArgs OnNextPacket;

        public PacketQueue(string threadName)
        {
            InboundPacketQueueProcessor = new Thread(new ThreadStart(Consumer))
            {
                Name = threadName
            };
            InboundPacketQueueProcessor.Start();
        }

        public void ReleaseResources()
        {
            while (UnprocessedPackets.Count > 0)
            {
                if (UnprocessedPackets.TryTake(out RawPacket q))
                {
                    q.ReleaseResources();
                }
            }
            UnprocessedPackets.Dispose();
            _readerTask.Dispose();
            UnprocessedPackets = null;
            _readerTask = null;
        }

        public void Shutdown()
        {
            UnprocessedPackets.CompleteAdding();
            Task.WaitAll(_readerTask);
        }
        public void Consumer()
        {
            _readerTask = Task.Factory.StartNew(() =>
            {
                foreach (RawPacket rp in UnprocessedPackets.GetConsumingEnumerable())
                {
                    OnNextPacket?.Invoke(rp);
                    rp.ReleaseResources();
                }
            }, TaskCreationOptions.LongRunning);
        }
        public void AddItem(RawPacket rp)
        {
            UnprocessedPackets.Add(rp);
        }
    }
}
