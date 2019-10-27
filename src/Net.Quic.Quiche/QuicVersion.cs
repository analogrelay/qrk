using System;

namespace Net.Quic.Quiche
{
    public struct QuicVersion: IEquatable<QuicVersion>
    {
        public static readonly QuicVersion Negotiate = new QuicVersion(0xbabababa);

        public uint Value { get; set; }

        public QuicVersion(uint value)
        {
            Value = value;
        }

        public bool Equals(QuicVersion other) => other.Value == Value;

        public override bool Equals(object obj) => obj is QuicVersion o && Equals(o);

        public override int GetHashCode() => Value.GetHashCode();
    }
}