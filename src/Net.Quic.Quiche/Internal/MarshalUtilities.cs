using System;
using System.Collections.Generic;
using System.Text;

namespace Net.Quic.Quiche.Internal
{
    internal static class MarshalUtilities
    {
        public unsafe static string Utf8NullTerminatedToString(IntPtr ptr)
        {
            byte* utf8Bytes = (byte*)ptr;
            byte* cur = utf8Bytes;
            var size = 0;
            while(*cur != 0)
            {
                size += 1;
                cur += 1;
            }
            return Encoding.UTF8.GetString(utf8Bytes, size);
        }
    }
}
