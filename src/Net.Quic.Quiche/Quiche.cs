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
    }
}
