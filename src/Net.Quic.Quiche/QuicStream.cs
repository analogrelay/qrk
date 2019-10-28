using System;

namespace Net.Quic.Quiche
{
    public class QuicStream
    {
        private readonly QuicConnection _connection;

        internal QuicStream(QuicConnection connection, long id)
        {
            _connection = connection;
            Id = id;
        }

        public long Id { get; }

        // TODO: Backpressure via pipes.
        public int Send(ReadOnlyMemory<byte> buf, bool fin)
        {
            return _connection.StreamSend(Id, buf, fin);
        }

        public int Receive(Memory<byte> buf, out bool fin)
        {
            return _connection.StreamRecv(Id, buf, out fin);
        }
    }
}