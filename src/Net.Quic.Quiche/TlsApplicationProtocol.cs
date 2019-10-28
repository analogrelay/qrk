using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Internal;

namespace Net.Quic.Quiche
{
    public struct TlsApplicationProtocol: IEquatable<TlsApplicationProtocol>
    {
        public static readonly TlsApplicationProtocol None = new TlsApplicationProtocol(Array.Empty<byte>());

        public TlsApplicationProtocol(ReadOnlyMemory<byte> value)
        {
            Value = value;
        }

        public TlsApplicationProtocol(string value)
            : this(Encoding.UTF8.GetBytes(value))
        {
        }

        public ReadOnlyMemory<byte> Value { get; }

        public bool Equals(TlsApplicationProtocol other) => other.Value.Span.SequenceEqual(Value.Span);

        public override bool Equals(object obj) => obj is TlsApplicationProtocol other && Equals(other);

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

        public override string ToString()
        {
            unsafe
            {
                fixed(byte* p = Value.Span)
                {
                    return Encoding.UTF8.GetString(p, Value.Length);
                }
            }
        }
    }
}
