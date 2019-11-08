using System.Net.Security;

namespace Net.Quic.Quiche.Features
{
    public interface ITlsApplicationProtocolFeature
    {
        SslApplicationProtocol ApplicationProtocol { get; }
    }
}
