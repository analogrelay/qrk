using System;
using System.Collections.Generic;

namespace Net.Quic.Quiche
{
    /// <summary>
    /// Helps to build a <see cref="QuicheConfig"/> value.
    /// </summary>
    public class QuicheConfigBuilder
    {
        private readonly QuicVersion _version;

        // TODO: Certs?

        public IList<ReadOnlyMemory<byte>> ApplicationProtocols { get; }
        public bool VerifyPeerCertificate { get; set; }
        public bool EnableTlsGrease { get; set; }
        public bool AllowLoggingOfSecrets { get; set; }
        public bool AllowEarlyData { get; set; }
        public TimeSpan IdleTimeout { get; set; } = TimeSpan.Zero;
        public long? MaxPacketSize { get; set; } = null;
        public long? InitialMaxData { get; set; } = null;
        public long? InitialMaxStreamDataBiDiLocal { get; set; } = null;
        public long? InitialMaxStreamDataBiDiRemote { get; set; } = null;
        public long? InitialMaxStreamDataUni { get; set; } = null;
        public long? InitialMaxStreamsBidi { get; set; } = null;
        public long? InitialMaxStreamsUni { get; set; } = null;
        public long? AckDelayExponent { get; set; } = null;
        public long? MaxAckDelay { get; set; } = null;
        public bool AllowActiveMigration { get; set; } = true;

        public QuicheConfigBuilder(QuicVersion version)
        {
            _version = version;
        }

        public QuicheConfig Build()
        {
            // Create a new config
            var configPtr = NativeMethods.quiche_config_new(_version.Value);
            try
            {
                ApplyConfiguration(configPtr);
            }
            catch
            {
                // Free the config and rethrow
                NativeMethods.quiche_config_free(configPtr);
                throw;
            }
            return new QuicheConfig(configPtr);
        }

        private void ApplyConfiguration(IntPtr configPtr)
        {
            if(ApplicationProtocols.Count > 0)
            {
                // Build a single buffer that is a set of non-empty 8-bit length prefixed strings:
                // For example "\x08http/1.1\x08http/0.9"
                TODO
            }
            //VerifyPeerCertificate
            //EnableTlsGrease
            //AllowLoggingOfSecrets
            //AllowEarlyData
            //IdleTimeout
            //MaxPacketSize
            //InitialMaxData
            //InitialMaxStreamDataBiDiLocal
            //InitialMaxStreamDataBiDiRemote
            //InitialMaxStreamDataUni
            //InitialMaxStreamsBidi
            //InitialMaxStreamsUni
            //AckDelayExponent
            //MaxAckDelay
            //AllowActiveMigration
        }
    }
}
