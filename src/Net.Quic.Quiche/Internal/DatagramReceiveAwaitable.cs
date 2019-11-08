using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Net.Quic.Quiche.Internal
{
    internal class DatagramReceiveAwaitable : ICriticalNotifyCompletion
    {
        private static readonly Action _callbackCompleted = () => { };

        private readonly PipeScheduler _ioScheduler;

        private Action _callback;
        private int _bytesTransferred;
        private EndPoint _endPoint;
        private SocketError _error;

        public DatagramReceiveAwaitable(PipeScheduler ioScheduler)
        {
            _ioScheduler = ioScheduler;
        }

        public DatagramReceiveAwaitable GetAwaiter() => this;
        public bool IsCompleted => ReferenceEquals(_callback, _callbackCompleted);

        public SocketReceiveFromResult GetResult()
        {
            Debug.Assert(ReferenceEquals(_callback, _callbackCompleted));

            _callback = null;

            if (_error != SocketError.Success)
            {
                throw new SocketException((int)_error);
            }

            return new SocketReceiveFromResult()
            {
                ReceivedBytes = _bytesTransferred,
                RemoteEndPoint = _endPoint
            };
        }

        public void OnCompleted(Action continuation)
        {
            if (ReferenceEquals(_callback, _callbackCompleted) ||
                ReferenceEquals(Interlocked.CompareExchange(ref _callback, continuation, null), _callbackCompleted))
            {
                Task.Run(continuation);
            }
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            OnCompleted(continuation);
        }

        public void Complete(int bytesTransferred, EndPoint endPoint, SocketError socketError)
        {
            _error = socketError;
            _bytesTransferred = bytesTransferred;
            _endPoint = endPoint;
            var continuation = Interlocked.Exchange(ref _callback, _callbackCompleted);

            if (continuation != null)
            {
                _ioScheduler.Schedule(state => ((Action)state)(), continuation);
            }
        }
    }
}
