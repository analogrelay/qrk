// Client sample based on quiche c-api client sample
// From: https://github.com/cloudflare/quiche/blob/d4e24ec88749629d15249f1e34bf95ae1b1b9f54/examples/client.c

namespace ClientSample
{
    internal class CommandLineOptions
    {
        public string Host { get; set; }
        public int Port { get; set; }
    }
}