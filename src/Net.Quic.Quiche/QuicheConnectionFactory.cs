using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Net.Quic.Quiche.Internal;

namespace Net.Quic.Quiche
{
    public class QuicheConnectionFactory : IConnectionFactory
    {
        private readonly QuicheConfig _config;
        private readonly IPEndPoint _localEndPoint;
        private readonly ILoggerFactory _loggerFactory;

        /// <summary>
        /// Creates a new connection factory with the specified configuration. Binds to
        /// any IP address and a random local port.
        /// </summary>
        /// <param name="config">A <see cref="QuicheConfig"/> object representing quiche configuration.</param>
        public QuicheConnectionFactory(QuicheConfig config)
            : this(config, new IPEndPoint(IPAddress.Any, 0), NullLoggerFactory.Instance)
        {
        }

        /// <summary>
        /// Creates a new connection factory with the specified configuration and local endpoint to bind to.
        /// </summary>
        /// <param name="config">A <see cref="QuicheConfig"/> object representing quiche configuration.</param>
        /// <param name="localEndPoint">The local endpoint to listen on.</param>
        /// <param name="loggerFactory">A <see cref="ILoggerFactory"/> to use for logging.</param>
        public QuicheConnectionFactory(QuicheConfig config, ILoggerFactory loggerFactory)
            : this(config, new IPEndPoint(IPAddress.Any, 0), loggerFactory)
        {
        }

        /// <summary>
        /// Creates a new connection factory with the specified configuration and local endpoint to bind to.
        /// </summary>
        /// <param name="config">A <see cref="QuicheConfig"/> object representing quiche configuration.</param>
        /// <param name="localEndPoint">The local endpoint to listen on.</param>
        /// <param name="loggerFactory">A <see cref="ILoggerFactory"/> to use for logging.</param>
        public QuicheConnectionFactory(QuicheConfig config, IPEndPoint localEndPoint, ILoggerFactory loggerFactory)
        {
            _config = config;
            _localEndPoint = localEndPoint;
            _loggerFactory = loggerFactory;
        }

        public async ValueTask<ConnectionContext> ConnectAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
        {
            // Create and bind socket
            var socket = new Socket(_localEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(_localEndPoint);

            // Create a connection and connect it
            var connection = new QuicheConnection(
                socket,
                _config,
                MemoryPool<byte>.Shared,
                PipeScheduler.ThreadPool,
                _loggerFactory.CreateLogger<QuicheConnection>());
            await connection.ConnectAsync(endpoint, cancellationToken);

            return connection;
        }

        private async Task ResolveDnsAsync(DnsEndPoint dnsEndPoint)
        {
            var address = await Dns.GetHostEntryAsync(dnsEndPoint.Host);
            if(address.AddressList.Length == 0)
            {
                throw new InvalidOperationException("Unable to resolve host name");
            }
        }
    }
}
