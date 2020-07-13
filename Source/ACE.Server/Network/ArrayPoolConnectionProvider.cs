using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ACE.Common.Connection
{
    public class ArrayPoolConnectionProvider : ConnectionProvider<ArrayPoolNetBuffer>
    {
        public ArrayPoolConnectionProvider(IPEndPoint listenPoint) : base(listenPoint) { }
        public override void Listen(string ListenThreadName, bool WithQueue, CancellationTokenSource CancelSignal, NetQueue<ArrayPoolNetBuffer>.OutputHandler dequeuedHandler, Action<ArrayPoolNetBuffer> directHandler = null)
        {
            base.Listen(ListenThreadName, WithQueue, CancelSignal, dequeuedHandler, directHandler);
            Interlocked.Increment(ref ZeroDoneSignal);
            try
            {
                DoneSignal.Reset();
                while (!CancelSignal.IsCancellationRequested)
                {
                    // allocate buffer
                    ArrayPoolNetBuffer state = new ArrayPoolNetBuffer()
                    {
                        WorkSocket = sock,
                        Peer = LocalSocket
                    };
                    // listen for available inbound data
                    IAsyncResult result = sock.BeginReceiveFrom(
                        state.Buffer,
                        0,
                        ArrayPoolNetBuffer.DEFAULT_BUFFER_SIZE,
                        SocketFlags.None,
                        ref LocalSocket,
                        new AsyncCallback(ReceiveCallback),
                        state);

                    // wait for inbound data
                    state.DoneSignal.WaitOne();

                    // notify
                    if (WithQueue)
                    {
                        Arrived.AddItem(state);
                    }
                    if (_ArrivedCallback != null)
                    {
                        _ArrivedCallback.Invoke(state);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            finally
            {
                Interlocked.Decrement(ref ZeroDoneSignal);
                DoneSignal.Set();
            }
        }
        //public bool Send(EndPoint ep, string data, bool WaitForSent = false)
        //{
        //    if (ep == null || data == null)
        //    {
        //        return false;
        //    }
        //    return Send(ep, Encoding.UTF8.GetBytes(data), WaitForSent);
        //}
        //public bool Send(EndPoint ep, byte[] data, bool WaitForSent = false)
        //{
        //    if (ep == null || data == null)
        //    {
        //        return false;
        //    }
        //    ArrayPoolNetBuffer state = new ArrayPoolNetBuffer
        //    {
        //        WorkSocket = sock,
        //        Peer = ep,
        //        Buffer = data,
        //        DataSize = data.Length
        //    };
        //    return Send(state, WaitForSent);
        //}
        public bool Send(ArrayPoolNetBuffer state, bool WaitForSent = false)
        {
            if (state == null || state.DataSize == 0)
            {
                return false;
            }
            try
            {
                state.DoneSignal.Reset();
                sock.BeginSendTo(state.Buffer, 0, state.DataSize, SocketFlags.None, state.Peer, (IAsyncResult state2) =>
                {
                    ArrayPoolNetBuffer state3 = (ArrayPoolNetBuffer)state2.AsyncState;
                    try
                    {
                        sock.EndSendTo(state2);
                    }
                    finally
                    {
                        state3.DoneSignal.Set();
                    }
                }, state);
                if (WaitForSent)
                {
                    return state.WaitForCompletion();
                }
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                state.DoneSignal.Set();
            }
        }
        private void ReceiveCallback(IAsyncResult ar)
        {
            ArrayPoolNetBuffer state = (ArrayPoolNetBuffer)ar.AsyncState;
            try
            {
                Socket sock = state.WorkSocket;
                byte[] buffer = state.Buffer;
                EndPoint peer = state.Peer;
                state.DataSize = sock.EndReceiveFrom(ar, ref peer);
                state.Peer = peer;
                state.Success = state.DataSize > 0 && state.Buffer != null && state.DataSize < state.Buffer.Length;
            }
            finally
            {
                state.DoneSignal.Set();
            }
        }
    }
}
