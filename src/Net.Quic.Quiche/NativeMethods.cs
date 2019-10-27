// Source: https://github.com/cloudflare/quiche/blob/d4e24ec88749629d15249f1e34bf95ae1b1b9f54/include/quiche.h

using System;
using System.Runtime.InteropServices;

namespace Net.Quic.Quiche
{
    internal static class NativeMethods
    {
        private const string DllName = "quiche";

        public const uint QUICHE_PROTOCOL_VERSION = 0xff000017;
        public const uint QUICHE_MAX_CONN_ID_LEN = 20;
        public const uint QUICHE_MIN_CLIENT_INITIAL_LEN = 200;

        public enum quiche_error
        {
            /// <summary>
            /// There is no more work to do.
            /// </summary>
            QUICHE_ERR_DONE = -1,

            /// <summary>
            /// The provided buffer is too short.
            /// </summary>
            QUICHE_ERR_BUFFER_TOO_SHORT = -2,

            /// <summary>
            /// The provided packet cannot be parsed because its version is unknown.
            /// </summary>
            QUICHE_ERR_UNKNOWN_VERSION = -3,

            /// <summary>
            /// The provided packet cannot be parsed because it contains an invalid frame.
            /// </summary>
            QUICHE_ERR_INVALID_FRAME = -4,

            /// <summary>
            /// The provided packet cannot be parsed.
            /// </summary>
            QUICHE_ERR_INVALID_PACKET = -5,

            /// <summary>
            /// The operation cannot be completed because the connection is in an invalid state.
            /// </summary>
            QUICHE_ERR_INVALID_STATE = -6,

            /// <summary>
            /// The operation cannot be completed because the stream is in an invalid state.
            /// </summary>
            QUICHE_ERR_INVALID_STREAM_STATE = -7,

            /// <summary>
            /// The peer's transport params cannot be parsed.
            /// </summary>
            QUICHE_ERR_INVALID_TRANSPORT_PARAM = -8,

            /// <summary>
            /// A cryptographic operation failed.
            /// </summary>
            QUICHE_ERR_CRYPTO_FAIL = -9,

            /// <summary>
            /// The TLS handshake failed.
            /// </summary>
            QUICHE_ERR_TLS_FAIL = -10,

            /// <summary>
            /// The peer violated the local flow control limits.
            /// </summary>
            QUICHE_ERR_FLOW_CONTROL = -11,

            /// <summary>
            /// The peer violated the local stream limits.
            /// </summary>
            QUICHE_ERR_STREAM_LIMIT = -12,

            /// <summary>
            /// The received data exceeds the stream's final size.
            /// </summary>
            QUICHE_ERR_FINAL_SIZE = -13,
        }

        [DllImport(DllName)]
        public static extern IntPtr quiche_version();

        public delegate void DebugLoggingCallback(IntPtr message, IntPtr state);
        [DllImport(DllName)]
        public static extern int quiche_enable_debug_logging(
            DebugLoggingCallback cb,
            IntPtr state);

        [DllImport(DllName)]
        public static extern IntPtr quiche_config_new(uint version);

        /// <summary>Configures whether to verify the peer's certificate.</summary>
        [DllImport(DllName)]
        public static extern void quiche_config_verify_peer(IntPtr config, bool v);

        /// <summary>Configures whether to send GREASE.</summary>
        [DllImport(DllName)]
        public static extern void quiche_config_grease(IntPtr config, bool v);

        /// <summary>Enables logging of secrets.</summary>
        [DllImport(DllName)]
        public static extern void quiche_config_log_keys(IntPtr config);

        /// <summary>Enables sending or receiving early data.</summary>
        [DllImport(DllName)]
        public static extern void quiche_config_enable_early_data(IntPtr config);

        /// <summary>Configures the list of supported application protocols.</summary>
        [DllImport(DllName)]
        public static extern int quiche_config_set_application_protos(
            IntPtr config,
            IntPtr protos,

            // Need nint/nuint :) https://github.com/dotnet/csharplang/issues/435
            UIntPtr protos_len);

        /// <summary>Sets the `idle_timeout` transport parameter.</summary>
        [DllImport(DllName)]
        public static extern void quiche_config_set_idle_timeout(IntPtr config, long v);

        /// <summary>Sets the `max_packet_size` transport parameter.</summary>
        [DllImport(DllName)]
        public static extern void quiche_config_set_max_packet_size(IntPtr config, long v);

        /// <summary>Sets the `initial_max_data` transport parameter.</summary>
        [DllImport(DllName)]
        public static extern void quiche_config_set_initial_max_data(IntPtr config, long v);

        /// <summary>Sets the `initial_max_stream_data_bidi_local` transport parameter.</summary>
        [DllImport(DllName)]
        public static extern void quiche_config_set_initial_max_stream_data_bidi_local(IntPtr config, long v);

        /// <summary>Sets the `initial_max_stream_data_bidi_remote` transport parameter.</summary>
        [DllImport(DllName)]
        public static extern void quiche_config_set_initial_max_stream_data_bidi_remote(IntPtr config, long v);

        /// <summary>Sets the `initial_max_stream_data_uni` transport parameter.</summary>
        [DllImport(DllName)]
        public static extern void quiche_config_set_initial_max_stream_data_uni(IntPtr config, long v);

        /// <summary>Sets the `initial_max_streams_bidi` transport parameter.</summary>
        [DllImport(DllName)]
        public static extern void quiche_config_set_initial_max_streams_bidi(IntPtr config, long v);

        /// <summary>Sets the `initial_max_streams_uni` transport parameter.</summary>
        [DllImport(DllName)]
        public static extern void quiche_config_set_initial_max_streams_uni(IntPtr config, long v);

        /// <summary>Sets the `ack_delay_exponent` transport parameter.</summary>
        [DllImport(DllName)]
        public static extern void quiche_config_set_ack_delay_exponent(IntPtr config, long v);

        /// <summary>Sets the `max_ack_delay` transport parameter.</summary>
        [DllImport(DllName)]
        public static extern void quiche_config_set_max_ack_delay(IntPtr config, long v);

        /// <summary>Sets the `disable_active_migration` transport parameter.</summary>
        [DllImport(DllName)]
        public static extern void quiche_config_set_disable_active_migration(IntPtr config, bool v);

        [DllImport(DllName)]
        internal static extern void quiche_config_free(IntPtr handle);
    }
}
