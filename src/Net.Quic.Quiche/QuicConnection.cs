using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Net.Quic.Quiche
{
    public class QuicConnection : IDisposable
    {
        private UdpClient _client;
        private readonly IPEndPoint _localEndPoint;
        private readonly QuicheConfig _config;
        private IPEndPoint _remoteEndPoint;
        private IntPtr _connection;
        private Task _receiveLoop;
        private Task _sendLoop;

        // TODO: If this library ever supports acting as a server, we'll need to make this configurable
        private bool _isClient = true;

        private long _bidiStreamCount = 0;
        private long _uniStreamCount = 0;

        private CancellationTokenSource _stopReceiving = new CancellationTokenSource();

        // We use a single-item channel to let the send loop sleep when it doesn't need to
        // run. Operations that will trigger new datagrams to become available will try to add
        // an item to the channel. If one is already there, it's a no-op but that's OK because
        // the loop will catch it on the next loop. If one isn't there, it'll start the send loop up again
        private Channel<bool> _sendSignal = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true
        });

        private readonly TaskCompletionSource<object> _establishedTcs = new TaskCompletionSource<object>();
        private readonly TaskCompletionSource<object> _earlyDataTcs = new TaskCompletionSource<object>();
        private readonly TaskCompletionSource<object> _closedTcs = new TaskCompletionSource<object>();

        /// <summary>
        /// Creates a new connectionusing the specified config 
        /// and a new auto-generated connection ID.
        /// </summary>
        /// <param name="localEndPoint">The local endpoint to bind to.</param>
        /// <param name="config">The <see cref="QuicheConfig"/> to use.</param>
        public QuicConnection(QuicheConfig config)
            : this(QuicConnectionId.NewId(), config)
        {
        }


        /// <summary>
        /// Creates a new connection using the specified config and connection ID.
        /// </summary>
        /// <param name="sourceConnectionId">The connection ID to use to identify the local end of the connection</param>
        /// <param name="config">The <see cref="QuicheConfig"/> to use.</param>
        public QuicConnection(QuicConnectionId sourceConnectionId, QuicheConfig config)
        {
            _config = config;
            _client = new UdpClient();
            SourceConnectionId = sourceConnectionId;
        }

        public IEnumerable<QuicStream> GetReadableStreams()
        {
            var iter = NativeMethods.quiche_conn_readable(_connection);
            try
            {
                while (NativeMethods.quiche_stream_iter_next(iter, out var id))
                {
                    yield return new QuicStream(this, (long)id);
                }
            }
            finally
            {
                NativeMethods.quiche_stream_iter_free(iter);
            }
        }

        /// <summary>
        /// Creates a new connection binding to the specified local endpoint, and using the specified config 
        /// and a new auto-generated connection ID.
        /// </summary>
        /// <param name="localEndPoint">The local endpoint to bind to.</param>
        /// <param name="config">The <see cref="QuicheConfig"/> to use.</param>
        public QuicConnection(IPEndPoint localEndPoint, QuicheConfig config)
            : this(localEndPoint, QuicConnectionId.NewId(), config)
        {
        }


        /// <summary>
        /// Creates a new connection binding to the specified local endpoint, using the specified config and connection ID.
        /// </summary>
        /// <param name="localEndPoint">The remote endpoint to connect to</param>
        /// <param name="sourceConnectionId">The connection ID to use to identify the local end of the connection</param>
        /// <param name="config">The <see cref="QuicheConfig"/> to use.</param>
        public QuicConnection(IPEndPoint localEndPoint, QuicConnectionId sourceConnectionId, QuicheConfig config)
        {
            _config = config;
            _localEndPoint = localEndPoint;
            SourceConnectionId = sourceConnectionId;
        }

        /// <summary>
        /// Gets the source connection ID in use
        /// </summary>
        public QuicConnectionId SourceConnectionId { get; }

        /// <summary>
        /// Gets the application protocol negotiated during the handshake. <see cref="TlsApplicationProtocol.None"/>.
        /// </summary>
        public TlsApplicationProtocol ApplicationProtocol { get; private set; } = TlsApplicationProtocol.None;

        public QuicStream CreateStream(QuicStreamType type)
        {
            var nextId = NextStreamId(type);
            return CreateStream(type, nextId);
        }

        public QuicStream CreateStream(QuicStreamType type, long desiredStreamId)
        {
            var streamType = desiredStreamId & 0x03;
            if (streamType != GetStreamType(type))
            {
                throw new InvalidOperationException($"The ID 0x{desiredStreamId:X} is not a valid {type} stream.");
            }
            return new QuicStream(this, desiredStreamId);
        }

        /// <summary>
        /// Connects to the remote endpoint and completes the QUIC handshake.
        /// </summary>
        /// <returns>A <see cref="Task"/> that completes when the QUIC connection has been established.</returns>
        public async Task ConnectAsync(EndPoint remoteEndPoint)
        {
            var serverName = await ConnectClientAsync(remoteEndPoint);
            _connection = CreateNativeConnection(serverName, SourceConnectionId, _config);

            // Start the loops.
            _receiveLoop = ProcessIncomingAsync();
            _sendLoop = FlushOutgoingAsync();

            // Signal that there's data to send
            SignalSend();

            // Return the task that will complete when established
            await _establishedTcs.Task;

            // Read the Application Protocol
            NativeMethods.quiche_conn_application_proto(_connection, out var appProtoPtr, out var appProtoLen);
            var buf = new byte[(int)appProtoLen];
            Marshal.Copy(appProtoPtr, buf, 0, (int)appProtoLen);
            ApplicationProtocol = new TlsApplicationProtocol(buf);
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        internal int StreamSend(long streamId, ReadOnlyMemory<byte> buf, bool fin)
        {
            unsafe
            {
                fixed (byte* pinned = buf.Span)
                {
                    var written = (int)NativeMethods.quiche_conn_stream_send(
                        _connection, (ulong)streamId, pinned, (UIntPtr)buf.Length, fin);
                    if (written < 0)
                    {
                        throw QuicheException.FromErrorCode((QuicheErrorCode)written);
                    }

                    // Signal the send loop that there's data to flush
                    SignalSend();

                    return written;
                }
            }
        }

        internal int StreamRecv(long streamId, Memory<byte> buf, out bool fin)
        {
            unsafe
            {
                fixed (byte* pinned = buf.Span)
                {
                    var received = (int)NativeMethods.quiche_conn_stream_recv(
                        _connection, (ulong)streamId, pinned, (UIntPtr)buf.Length, out fin);
                    if (received == (int)QuicheErrorCode.Done)
                    {
                        received = 0;
                    }
                    else if (received < 0)
                    {
                        throw QuicheException.FromErrorCode((QuicheErrorCode)received);
                    }
                    return received;
                }
            }
        }

        private void SignalSend()
        {
            // We don't care about the result. If it fails, either:
            // 1. There is already an item, meaning we're already going to send some data.
            // 2. The channel is closed and none of this matters.
            if (_sendSignal.Writer.TryWrite(true))
            {
                Log("Queuing future send.");
            }
            else
            {
                Log("A send is already queued.");
            }
        }

        private async Task ProcessIncomingAsync()
        {
            // Keep looping until we're told we're closed
            // If we're not closed, we expect at least one more UDP datagram, so we keep looping.
            while (!_closedTcs.Task.IsCompleted)
            {
                // Recieve from the socket
                var recv = await _client.ReceiveAsync();
                Log($"Received incoming datagram of {recv.Buffer.Length} bytes.");

                // Send it to quiche
                var done = ProcessIncomingDatagram(recv.Buffer);
                if (done == (int)QuicheErrorCode.Done)
                {
                    Log("Finished recieving.");
                }
                else if (done < 0)
                {
                    // Some kind of error
                    var ex = QuicheException.FromErrorCode((QuicheErrorCode)done);
                    Log($"Error processing incoming datagram: {ex.Message}");
                    throw ex;
                }
                else
                {
                    Log($"Received {done} bytes.");
                }

                // Signal we may have more data to send
                SignalSend();

                // Update TCS state
                UpdateTaskState();
            }
        }

        private async Task FlushOutgoingAsync()
        {
            while (await _sendSignal.Reader.WaitToReadAsync())
            {
                // The actual value doesn't matter.
                if (!_sendSignal.Reader.TryRead(out _))
                {
                    continue;
                }
                // Ask quiche for a datagram.
                var buf = new byte[Constants.MAX_DATAGRAM_SIZE];
                var written = GetOutgoingDatagram(buf);

                if (written == (int)QuicheErrorCode.Done)
                {
                    Log("Finished sending.");
                    continue;
                }
                else if (written < 0)
                {
                    // Some kind of error
                    var ex = QuicheException.FromErrorCode((QuicheErrorCode)written);
                    Log($"Error preparing outgoing datagram: {ex.Message}");
                    throw ex;
                }

                // Send the datagram
                Log($"Sending datagram of {written} bytes.");
                var sent = await _client.SendAsync(buf, written);
                if (sent != written)
                {
                    throw new InvalidOperationException("Unable to send a complete datagram!");
                }

                // TODO: Handle timeout.

                // Update TCS state
                UpdateTaskState();
            }

            // The channel is closed.
        }

        private async Task<string> ConnectClientAsync(EndPoint remoteEndPoint)
        {
            string serverName;
            switch (remoteEndPoint)
            {
                case IPEndPoint ipEndPoint:
                    serverName = ipEndPoint.Address.ToString();
                    _remoteEndPoint = ipEndPoint;
                    break;
                case DnsEndPoint dnsEndPoint:
                    var hostAddresses = await Dns.GetHostAddressesAsync(dnsEndPoint.Host);
                    if (hostAddresses.Length == 0)
                    {
                        throw new InvalidOperationException($"Unable to resolve DNS name: {dnsEndPoint.Host}");
                    }
                    serverName = dnsEndPoint.Host;
                    _remoteEndPoint = new IPEndPoint(hostAddresses[0], dnsEndPoint.Port);
                    break;
                default:
                    throw new NotSupportedException($"Cannot connect to endpoint of type {remoteEndPoint.GetType().FullName}.");
            }
            _client = new UdpClient(_remoteEndPoint.AddressFamily);
            _client.Connect(_remoteEndPoint);
            return serverName;
        }

        private void UpdateTaskState()
        {
            if (!_establishedTcs.Task.IsCompleted && NativeMethods.quiche_conn_is_established(_connection))
            {
                _establishedTcs.TrySetResult(null);
            }
            if (!_closedTcs.Task.IsCompleted && NativeMethods.quiche_conn_is_closed(_connection))
            {
                _closedTcs.TrySetResult(null);
                _sendSignal.Writer.TryComplete();
            }
            if (!_earlyDataTcs.Task.IsCompleted && NativeMethods.quiche_conn_is_in_early_data(_connection))
            {
                _earlyDataTcs.TrySetResult(null);
            }
        }

        private unsafe int ProcessIncomingDatagram(byte[] buffer)
        {
            if (buffer.Length > 0)
            {
                Log($"Giving Quiche {buffer.Length} bytes.");
                // Pin the buffer and give it to Quiche
                fixed (byte* ptr = buffer)
                {
                    return (int)NativeMethods.quiche_conn_recv(_connection, ptr, (UIntPtr)buffer.Length);
                }
            }
            return 0;
        }

        private unsafe int GetOutgoingDatagram(byte[] buffer)
        {
            // Pin the buffer and ask Quiche to fill it.
            fixed (byte* ptr = buffer)
            {
                return (int)NativeMethods.quiche_conn_send(_connection, ptr, (UIntPtr)buffer.Length);
            }
        }

        private unsafe static IntPtr CreateNativeConnection(string serverName, QuicConnectionId scid, QuicheConfig config)
        {
            // PERF: We could stackalloc this if it's small enough.
            var buf = new byte[Encoding.UTF8.GetByteCount(serverName) + 1];
            Encoding.UTF8.GetBytes(serverName, 0, serverName.Length, buf, 0);
            buf[buf.Length - 1] = 0; // Null terminator.
            fixed (byte* ptr = buf)
            {
                fixed (byte* scidPtr = scid.Value.Span)
                {
                    return NativeMethods.quiche_connect(
                        ptr,
                        scidPtr,
                        (UIntPtr)scid.Value.Length,
                        config);
                }
            }
        }

        private static void Log(string message)
        {
            // TODO: Super-simple logging
            Console.Error.WriteLine(message);
        }

        private long NextStreamId(QuicStreamType type)
        {
            long streamId;
            if (type == QuicStreamType.Bidirectional)
            {
                streamId = Interlocked.Increment(ref _bidiStreamCount) - 1;
            }
            else
            {
                streamId = Interlocked.Increment(ref _uniStreamCount) - 1;
            }

            return (streamId << 2) | GetStreamType(type);
        }

        private byte GetStreamType(QuicStreamType type)
        {
            if (_isClient)
            {
                if (type == QuicStreamType.Bidirectional)
                {
                    return 0;
                }
                else
                {
                    return 2;
                }
            }
            else
            {
                if (type == QuicStreamType.Bidirectional)
                {
                    return 1;
                }
                else
                {
                    return 3;
                }
            }
        }
    }
}
