using System;
using Net.Quic.Quiche;

namespace qrk
{
    class Program
    {
        static void Main(string[] args)
        {
            Quiche.EnableDebugLogging(Console.WriteLine);
            Console.WriteLine($"Quiche Version: {Quiche.Version}");

            var config = QuicheConfigBuilder.Create(42);
            Console.WriteLine("Created config");
        }
    }
}
