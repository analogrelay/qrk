// Client sample based on quiche c-api client sample
// From: https://github.com/cloudflare/quiche/blob/d4e24ec88749629d15249f1e34bf95ae1b1b9f54/examples/client.c

using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Net.Quic.Quiche;
using Net.Quic.Quiche.Features;

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
            var hostName = args[0];
            if (!int.TryParse(args[1], out var port))
            {
                Console.Error.WriteLine($"Invalid port number: {args[1]}.");
                return 1;
            }

            using var host = Host.CreateDefaultBuilder()
                .ConfigureLogging(logging =>
                {
                    logging.AddFilter("ClientSample", LogLevel.Trace);
                    logging.AddFilter("Net.Quic.Quiche", LogLevel.Trace);
                })
                .ConfigureServices(services =>
                {
                    services.Configure<CommandLineOptions>(options =>
                    {
                        options.Host = hostName;
                        options.Port = port;
                    });
                    services.AddSingleton<Application>();
                })
                .Build();
            await host.StartAsync();

            var app = host.Services.GetRequiredService<Application>();
            var exitCode = await app.RunAsync();
            await host.StopAsync();

            return exitCode;
        }
    }
}
