using System;
using System.IO.Pipelines;
using System.Net.Sockets;

namespace Net.Quic.Quiche.Internal
{
    internal class DatagramReceiver
    {
        private readonly Socket _socket;
        private readonly SocketAsyncEventArgs _eventArgs = new SocketAsyncEventArgs();
        private readonly DatagramReceiveAwaitable _awaitable;

        public DatagramReceiver(Socket socket, PipeScheduler scheduler)
        {
            _socket = socket;
            _awaitable = new DatagramReceiveAwaitable(scheduler);
            _eventArgs.UserToken = _awaitable;
            _eventArgs.Completed += (_, e) => ((DatagramReceiveAwaitable)e.UserToken).Complete(e.BytesTransferred, e.RemoteEndPoint, e.SocketError);
        }

        public DatagramReceiveAwaitable ReceiveFromAsync(Memory<byte> buffer)
        {
#if NETCOREAPP3_0
            _eventArgs.SetBuffer(buffer);
#else
            var segment = buffer.GetArray();

            _eventArgs.SetBuffer(segment.Array, segment.Offset, segment.Count);
#endif
            if (!_socket.ReceiveAsync(_eventArgs))
            {
                _awaitable.Complete(_eventArgs.BytesTransferred, _eventArgs.RemoteEndPoint, _eventArgs.SocketError);
            }

            return _awaitable;
        }
    }
}
