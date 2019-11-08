using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Net.Quic.Quiche.Features;

namespace Net.Quic.Quiche.Internal
{
    internal class QuicheConnection : ConnectionContext, IDisposable, ITlsApplicationProtocolFeature
    {
        private IntPtr _conn;
        private QuicConnectionId _sourceConnectionId;
        private EndPoint _remoteEndPoint;
        private CancellationTokenSource _stopReceiving = new CancellationTokenSource();

        private readonly Socket _socket;
        private readonly DatagramReceiver _receiver;
        private readonly DatagramSender _sender;
        private readonly Waker _sendWaker = new Waker();

        private readonly QuicheConfig _config;
        private readonly MemoryPool<byte> _pool;
        private readonly ILogger<QuicheConnection> _logger;

        private readonly TaskCompletionSource<object> _establishedTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

        public QuicheConnection(Socket socket, QuicheConfig config, MemoryPool<byte> pool, PipeScheduler scheduler, ILogger<QuicheConnection> logger)
        {
            _socket = socket;
            _receiver = new DatagramReceiver(_socket, scheduler);
            _sender = new DatagramSender(_socket, scheduler);

            _config = config;
            _pool = pool;
            _logger = logger;

            Features.Set<ITlsApplicationProtocolFeature>(this);
        }

        ~QuicheConnection()
        {
            Dispose(false);
        }

        public override string ConnectionId { get; set; }
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override IDictionary<object, object> Items { get; set; } = new ConnectionItems();
        public override IDuplexPipe Transport
        {
            get => throw new NotSupportedException("QUIC connections cannot be read from or written to directly. Use 'IStreamsFeature' to access individual streams.");
            set => throw new NotSupportedException("QUIC connections cannot be read from or written to directly. Use 'IStreamsFeature' to access individual streams.");
        }

        public SslApplicationProtocol ApplicationProtocol { get; private set; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _socket.Dispose();
            }

            if (_conn != IntPtr.Zero)
            {
                NativeMethods.quiche_conn_free(_conn);
            }
        }

        internal Task ConnectAsync(EndPoint remoteEndPoint, CancellationToken cancellationToken)
        {
            var serverName = remoteEndPoint switch
            {
                IPEndPoint ip => ip.Address.ToString(),
                DnsEndPoint dns => dns.Host,
                _ => string.Empty
            };

            _sourceConnectionId = QuicConnectionId.NewId();
            ConnectionId = _sourceConnectionId.ToString();

            // TODO: Quiche actually wants a null pointer for serverName if not specified...
            var serverNameBytes = Encoding.UTF8.GetBytes(serverName);
            unsafe
            {
                fixed (byte* serverNamePtr = serverNameBytes)
                fixed (byte* scidPtr = _sourceConnectionId.Value.Span)
                {
                    _conn = NativeMethods.quiche_connect(serverNamePtr, scidPtr, (UIntPtr)_sourceConnectionId.Value.Length, _config);
                }
            }

            _remoteEndPoint = remoteEndPoint;

            _ = ExecuteAsync();

            return _establishedTcs.Task;
        }

        private void Establish()
        {
            NativeMethods.quiche_conn_application_proto(_conn, out var buf, out var bufLen);
            var appProto = new byte[(int)bufLen];
            Marshal.Copy(buf, appProto, 0, (int)bufLen);
            ApplicationProtocol = new SslApplicationProtocol(appProto);

            _establishedTcs.TrySetResult(null);
        }

        private async Task ExecuteAsync()
        {
            try
            {
                var sendLoop = Task.Run(() => SendDatagramsAsync());
                var receiveLoop = Task.Run(() => ReceiveDatagramsAsync());

                // Wait for a loop to terminate
                var stoppedTask = await Task.WhenAny(sendLoop, receiveLoop);
                var runningTask = stoppedTask == sendLoop ? receiveLoop : sendLoop;

                // Close the socket and stop the waker
                _socket.Close();
                _sendWaker.Stop();

                if (stoppedTask.IsCompletedSuccessfully)
                {
                    // Graceful shutdown, just wait for the other loop to shut down.
                    await runningTask;
                }
                else
                {
                    // Ungraceful shutdown, manifest the exception
                    stoppedTask.GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while executing transport.");
            }
        }

        private async Task SendDatagramsAsync()
        {
            try
            {
                while (await _sendWaker)
                {
                    // Get a buffer
                    using (var buffer = _pool.Rent(4096))
                    {
                        int res;

                        // Fill it with QUICy goodness
                        unsafe
                        {
                            fixed (byte* buf = buffer.Memory.Span)
                            {
                                res = (int)NativeMethods.quiche_conn_send(_conn, buf, (UIntPtr)buffer.Memory.Length);
                            }
                        }

                        if (res == (int)QuicheErrorCode.Done)
                        {
                            _logger.LogDebug("Finished sending frames, sleeping until more frames are available.");
                            continue;
                        }
                        else if (res < 0)
                        {
                            throw QuicheException.FromErrorCode((QuicheErrorCode)res);
                        }

                        // Send the buffer to the socket
                        _logger.LogTrace("Sending UDP Datagram of {Count} bytes...", res);
                        await _sender.SendAsync(buffer.Memory.Slice(0, res), _remoteEndPoint);
                        _logger.LogDebug("Sent UDP Datagram of {Count} bytes.", res);
                    }

                    if (NativeMethods.quiche_conn_is_closed(_conn))
                    {
                        // We're closed, stop the loop
                        return;
                    }
                    if (NativeMethods.quiche_conn_is_established(_conn))
                    {
                        Establish();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Send loop failed with error.");
                throw;
            }
        }

        private async Task ReceiveDatagramsAsync()
        {
            // Why aren't we using pipelines here?
            // Well, we're recieving datagrams of mostly fixed size, and each datagram could be
            // from a different remote endpoint (particularly in a server setting). 

            try
            {
                while (_stopReceiving.IsCancellationRequested)
                {
                    int res;

                    // 4K is likely to be large enough for any datagram.
                    // For now, if it IS insufficient, we blow up :).
                    using (var buffer = _pool.Rent(4096))
                    {
                        // Receive a datagram
                        _logger.LogTrace("Receiving UDP Datagram...");
                        var result = await _receiver.ReceiveFromAsync(buffer.Memory);
                        _logger.LogDebug("Received UDP Datagram of {Count} bytes.", result.ReceivedBytes);

                        // Give it to quiche
                        unsafe
                        {
                            fixed (byte* buf = buffer.Memory.Span)
                            {
                                res = (int)NativeMethods.quiche_conn_recv(_conn, buf, (UIntPtr)result.ReceivedBytes);
                            }
                        }

                        if (res >= 0 && res != result.ReceivedBytes)
                        {
                            throw new InvalidOperationException("Expected to process as many bytes as was received!");
                        }
                    }

                    if (res == (int)QuicheErrorCode.Done)
                    {
                        // We've finished receiving.
                        _logger.LogDebug("Done receiving QUIC packets.");
                    }
                    else if (res < 0)
                    {
                        throw QuicheException.FromErrorCode((QuicheErrorCode)res);
                    }
                    else
                    {
                        _logger.LogDebug("Processed {Count} bytes of UDP datagram.", res);
                    }

                    if (NativeMethods.quiche_conn_is_closed(_conn))
                    {
                        // We're closed, stop the loop
                        return;
                    }
                    if (NativeMethods.quiche_conn_is_established(_conn))
                    {
                        Establish();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Receive loop failed with error.");
                throw;
            }
        }
    }
}
