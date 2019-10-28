using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Extensions.Internal;

namespace Net.Quic.Quiche
{
    public struct QuicConnectionId: IEquatable<QuicConnectionId>
    {
        private static readonly RNGCryptoServiceProvider _rng = new RNGCryptoServiceProvider();

        public ReadOnlyMemory<byte> Value { get; }

        public QuicConnectionId(ReadOnlyMemory<byte> value)
        {
            Value = value;
        }

        public static QuicConnectionId NewId()
        {
            var buf = new byte[NativeMethods.QUICHE_MAX_CONN_ID_LEN];
            _rng.GetBytes(buf);
            return new QuicConnectionId(buf);
        }

        private static char[] _hexChars = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f' };
        public override string ToString()
        {
            var chrs = new char[Value.Length * 2];
            for (var i = 0; i < Value.Length; i++)
            {
                var lo = Value.Span[i] & 0x0f;
                var hi = Value.Span[i] >> 4;
                chrs[i * 2] = _hexChars[hi];
                chrs[(i * 2) + 1] = _hexChars[lo];
            }
            return new string(chrs);
        }

        public bool Equals(QuicConnectionId other)
        {
            // PERF: In CoreFX we trust.
            return Value.Span.SequenceEqual(other.Value.Span);
        }

        public override bool Equals(object obj) => obj is QuicConnectionId other && Equals(other);

        public override int GetHashCode()
        {
            var combiner = HashCodeCombiner.Start();

            // This will truncate if the value length isn't a multiple of 4, but ¯\_(ツ)_/¯
            var asInts = MemoryMarshal.Cast<byte, int>(Value.Span);
            foreach(var val in asInts)
            {
                combiner.Add(val);
            }

            return combiner.CombinedHash;
        }
    }
}
