using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace Net.Quic.Quiche.Internal
{
    internal class DatagramSender
    {
        private readonly Socket _socket;
        private readonly SocketAsyncEventArgs _eventArgs = new SocketAsyncEventArgs();
        private readonly SocketAwaitable _awaitable;

        public DatagramSender(Socket socket, PipeScheduler scheduler)
        {
            _socket = socket;
            _awaitable = new SocketAwaitable(scheduler);
            _eventArgs.UserToken = _awaitable;
            _eventArgs.Completed += (_, e) => ((SocketAwaitable)e.UserToken).Complete(e.BytesTransferred, e.SocketError);
        }

        public SocketAwaitable SendAsync(ReadOnlyMemory<byte> memory, EndPoint remoteEndPoint)
        {
            _eventArgs.RemoteEndPoint = remoteEndPoint;

            // The BufferList getter is much less expensive then the setter.
            if (_eventArgs.BufferList != null)
            {
                _eventArgs.BufferList = null;
            }

#if NETCOREAPP
            _eventArgs.SetBuffer(MemoryMarshal.AsMemory(memory));
#else
            var segment = memory.GetArray();

            _eventArgs.SetBuffer(segment.Array, segment.Offset, segment.Count);
#endif
            if (!_socket.SendAsync(_eventArgs))
            {
                _awaitable.Complete(_eventArgs.BytesTransferred, _eventArgs.SocketError);
            }

            return _awaitable;
        }
    }
}
