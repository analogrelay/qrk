using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Net.Quic.Quiche;
using Net.Quic.Quiche.Features;

namespace ClientSample
{
    internal class Application
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly IOptions<CommandLineOptions> _commandLine;
        private readonly ILogger<Application> _logger;

        public Application(ILoggerFactory loggerFactory, IOptions<CommandLineOptions> commandLine)
        {
            _loggerFactory = loggerFactory;
            _commandLine = commandLine;
            _logger = _loggerFactory.CreateLogger<Application>();
        }

        public async Task<int> RunAsync()
        {
            Quiche.EnableDebugLogging(_loggerFactory.CreateLogger("ClientSample.QuicheDebug"));
            _logger.LogInformation("Quiche Version: {Version}", Quiche.Version);

            // Prepare Quiche Config
            using var config =
                new QuicheConfigBuilder(QuicVersion.Negotiate)
                {
                    IdleTimeout = TimeSpan.FromSeconds(5),
                    MaxPacketSize = 1350,
                    InitialMaxData = 10_000_000,
                    InitialMaxStreamDataBiDiLocal = 1_000_000,
                    InitialMaxStreamDataUni = 1_000_000,
                    InitialMaxStreamsBidi = 100,
                    InitialMaxStreamsUni = 100,
                    AllowActiveMigration = false,
                    EnableSslKeyLogging = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SSLKEYLOGFILE"))
                }
                .AddApplicationProtocol("http/0.9")
                .Build();

            var connectionFactory = new QuicheConnectionFactory(config);
            var connection = await connectionFactory.ConnectAsync(new DnsEndPoint(_commandLine.Value.Host, _commandLine.Value.Port));
            _logger.LogInformation("Connected (SCID: {ConnectionId}).", connection.ConnectionId);

            var alpn = connection.Features.Get<ITlsApplicationProtocolFeature>();
            if (alpn != null)
            {
                _logger.LogInformation("ALPN negotiated protocol: '{ApplicationProtocol}'.", alpn.ApplicationProtocol);
            }

            //// Create a stream
            //var stream = connection.CreateStream(QuicStreamType.Bidirectional, 4);
            //Console.WriteLine("Sending HTTP/0.9 request...");
            //stream.Send(RequestContent, fin: true);
            //Console.WriteLine("Sent");

            //// Keep receiving until we can't receive any more.
            //while (true)
            //{
            //    foreach (var readable in connection.GetReadableStreams())
            //    {
            //        var buf = new byte[1024];
            //        int recv;
            //        while ((recv = readable.Receive(buf, out var fin)) != 0)
            //        {
            //            var str = Encoding.UTF8.GetString(buf, 0, recv);
            //            Console.WriteLine($"Received on {readable.Id}: {str}");
            //        }
            //    }
            //}

            return 0;
        }
    }
}
