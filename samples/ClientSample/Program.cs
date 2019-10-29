// Client sample based on quiche c-api client sample
// From: https://github.com/cloudflare/quiche/blob/d4e24ec88749629d15249f1e34bf95ae1b1b9f54/examples/client.c

using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Net.Quic.Quiche;

namespace ClientSample
{
    class Program
    {
        private static readonly byte[] RequestContent = Encoding.UTF8.GetBytes("GET /index.html\r\n");

        static async Task<int> Main(string[] args)
        {
            // Arg Parsing
            if (args.Length != 2)
            {
                Console.Error.WriteLine($"Usage: {typeof(Program).Assembly.GetName().Name} <host> <port>");
                return 1;
            }
            var host = args[0];
            if (!int.TryParse(args[1], out var port))
            {
                Console.Error.WriteLine($"Invalid port number: {args[1]}.");
                return 1;
            }

            //Quiche.EnableDebugLogging(Console.Error.WriteLine);
            Console.WriteLine($"Quiche Version: {Quiche.Version}");

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
            Console.WriteLine("Created config");

            var connection = new QuicConnection(config);
            Console.WriteLine($"Connecting (SCID: {connection.SourceConnectionId}) ...");
            await connection.ConnectAsync(new DnsEndPoint(host, port));

            Console.WriteLine($"Connected. ALPN negotiated protocol: '{connection.ApplicationProtocol}'.");

            // Create a stream
            var stream = connection.CreateStream(QuicStreamType.Bidirectional, 4);
            Console.WriteLine("Sending HTTP/0.9 request...");
            stream.Send(RequestContent, fin: true);
            Console.WriteLine("Sent");

            // Keep receiving until we can't receive any more.
            while (true)
            {
                foreach (var readable in connection.GetReadableStreams())
                {
                    var buf = new byte[1024];
                    int recv;
                    while ((recv = readable.Receive(buf, out var fin)) != 0)
                    {
                        var str = Encoding.UTF8.GetString(buf, 0, recv);
                        Console.WriteLine($"Received on {readable.Id}: {str}");
                    }
                }
            }
        }
    }
}
