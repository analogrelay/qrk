using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Net.Quic.Quiche.Internal;

namespace Net.Quic.Quiche
{
    /// <summary>
    /// Gets global context/state from Quiche
    /// </summary>
    public static class Quiche
    {
        private static readonly Lazy<string> _version = new Lazy<string>(() =>
            MarshalUtilities.Utf8NullTerminatedToString(NativeMethods.quiche_version()));

        public static string Version => _version.Value;

        /// <summary>
        /// Enables debug logging in Quiche. This method will NOT keep <paramref name="logger"/> alive, so it's
        /// expected that the caller stores it somewhere useful. When <paramref name="logger"/> is freed, the callback will
        /// no longer function.
        /// </summary>
        /// <param name="logger">The logger to log to.</param>
        public static void EnableDebugLogging(ILogger logger)
        {
            static void NativeCallback(IntPtr str, IntPtr state)
            {
                var logger = (ILogger)((GCHandle)state).Target;
                if(logger != null)
                {
                    var message = MarshalUtilities.Utf8NullTerminatedToString(str);
                    logger.LogDebug(message);
                }
            }

            var handle = GCHandle.Alloc(logger, GCHandleType.Weak);
            NativeMethods.quiche_enable_debug_logging(NativeCallback, (IntPtr)handle);
        }
    }
}
