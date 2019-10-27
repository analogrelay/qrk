using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        public bool EnableEarlyData { get; set; }
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

        private void ApplyConfiguration(IntPtr config)
        {
            if (ApplicationProtocols.Count > 0)
            {
                // Build a single buffer that is a set of non-empty 8-bit length prefixed strings:
                // For example "\x08http/1.1\x08http/0.9"
                var totalLength = ApplicationProtocols.Count + ApplicationProtocols.Sum(p => p.Length);

                // PERF: Could probably stackalloc this if it's small enough
                // I believe quiche copies the data out as soon as the function is called, so it's safe to do so.
                var buf = new byte[totalLength];
                var offset = 0;
                foreach (var protocol in ApplicationProtocols)
                {
                    if (protocol.Length > byte.MaxValue)
                    {
                        var protocolName = Encoding.UTF8.GetString(protocol.ToArray());
                        var message = $"Application Protocol value is too long: {protocolName}";
                        throw new InvalidOperationException(message);
                    }
                    buf[offset] = (byte)protocol.Length;
                    protocol.CopyTo(buf.AsMemory(offset + 1));
                    offset += protocol.Length + 1;
                }

                // Set the value on the internal config struct
                var err = NativeMethods.quiche_config_set_application_protos(config, buf, (UIntPtr)buf.Length);
                if (err != 0)
                {
                    throw QuicheException.FromErrorCode((QuicheErrorCode)err);
                }
            }

            NativeMethods.quiche_config_verify_peer(config, VerifyPeerCertificate);
            NativeMethods.quiche_config_grease(config, EnableTlsGrease);
            NativeMethods.quiche_config_set_disable_active_migration(config, !AllowActiveMigration);

            if (AllowLoggingOfSecrets)
            {
                NativeMethods.quiche_config_log_keys(config);
            }

            if (EnableEarlyData)
            {
                NativeMethods.quiche_config_enable_early_data(config);
            }

            if (IdleTimeout != TimeSpan.Zero)
            {
                NativeMethods.quiche_config_set_idle_timeout(config, (long)IdleTimeout.TotalMilliseconds);
            }

            if (MaxPacketSize != null)
            {
                NativeMethods.quiche_config_set_max_packet_size(config, MaxPacketSize.Value);
            }

            if (InitialMaxData != null)
            {
                NativeMethods.quiche_config_set_initial_max_data(config, InitialMaxData.Value);
            }

            if (InitialMaxStreamDataBiDiLocal != null)
            {
                NativeMethods.quiche_config_set_initial_max_stream_data_bidi_local(config, InitialMaxStreamDataBiDiLocal.Value);
            }

            if (InitialMaxStreamDataBiDiRemote != null)
            {
                NativeMethods.quiche_config_set_initial_max_stream_data_bidi_remote(config, InitialMaxStreamDataBiDiRemote.Value);
            }

            if (InitialMaxStreamDataUni != null)
            {
                NativeMethods.quiche_config_set_initial_max_stream_data_uni(config, InitialMaxStreamDataUni.Value);
            }

            if (InitialMaxStreamsBidi != null)
            {
                NativeMethods.quiche_config_set_initial_max_streams_bidi(config, InitialMaxStreamsBidi.Value);
            }

            if (InitialMaxStreamsUni != null)
            {
                NativeMethods.quiche_config_set_initial_max_streams_uni(config, InitialMaxStreamsUni.Value);
            }

            if (AckDelayExponent != null)
            {
                NativeMethods.quiche_config_set_ack_delay_exponent(config, AckDelayExponent.Value);
            }

            if (MaxAckDelay != null)
            {
                NativeMethods.quiche_config_set_max_ack_delay(config, MaxAckDelay.Value);
            }
        }
    }
}
