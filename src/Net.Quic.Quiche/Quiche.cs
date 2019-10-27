using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

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

        public static void EnableDebugLogging(Action<string> callback)
        {
            static void NativeCallback(IntPtr str, IntPtr state)
            {
                var callbackHandle = GCHandle.FromIntPtr(state);
                var callback = (Action<string>)callbackHandle.Target;
                var message = MarshalUtilities.Utf8NullTerminatedToString(str);
                callback(message);
            }

            // PERF: This will leak the callback, which is OK for now.
            var handle = GCHandle.Alloc(callback, GCHandleType.Normal);
            NativeMethods.quiche_enable_debug_logging(NativeCallback, GCHandle.ToIntPtr(handle));
        }
    }
}
