using ACE.Common;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ACE.Common.Connection
{
    public class NetQueue<T> : INeedCleanup where T : INeedCleanup
    {
        private BlockingCollection<T> Buffer = new BlockingCollection<T>();
        private Task _readerTask;
        private readonly Thread ProcessorThread = null;
        public int QueueLength => Buffer.Count;
        public delegate void OutputHandler(T rp);

        public NetQueue(string threadName, OutputHandler handler)
        {
            ProcessorThread = new Thread(new ParameterizedThreadStart(Consumer))
            {
                Name = threadName
            };
            ProcessorThread.Start(handler);
        }

        public void ReleaseResources()
        {
            while ((Buffer?.Count ?? -1) > 0)
            {
                T q = default(T);
                if (Buffer?.TryTake(out q) ?? false)
                {
                    q?.ReleaseResources();
                }
            }
            //Next = null;
            Buffer?.Dispose();
            _readerTask?.Dispose();
            Buffer = null;
            _readerTask = null;
        }

        public void Shutdown()
        {
            Buffer.CompleteAdding();
            Task.WaitAll(_readerTask);
        }
        public void Consumer(object state)
        {
            OutputHandler handler = (OutputHandler)state;
            if (handler == null)
            {
                throw new Exception("queue output handler is null");
            }
            _readerTask = Task.Factory.StartNew(() =>
            {
                foreach (T rp in Buffer.GetConsumingEnumerable())
                {
                    handler(rp);
                    //rp.ReleaseResources();
                }
            }, TaskCreationOptions.LongRunning);
        }
        public void AddItem(T rp)
        {
            Buffer.Add(rp);
        }
    }
}
