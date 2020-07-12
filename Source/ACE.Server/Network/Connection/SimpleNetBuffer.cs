using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ACE.Server.Network.Connection
{
    public class SimpleNetBuffer : NetBuffer
    {
        public SimpleNetBuffer(bool AllocateDefaultBuffer) : base(AllocateDefaultBuffer) { }
        public SimpleNetBuffer() { }
        public ManualResetEvent DoneSignal { get; private set; } = new ManualResetEvent(false);
        public bool WaitForCompletion()
        {
            DoneSignal.WaitOne();
            return Success;
        }
    }
}
