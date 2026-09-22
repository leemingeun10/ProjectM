using System;

namespace Game.Sim.Stroke
{
    // Deterministic fixed-point number, 3 decimal digits of precision (1.000 == Scale).
    // Game.sim must never use float/double; all trajectory math routes through this type.
    public readonly struct Fixed : IEquatable<Fixed>, IComparable<Fixed>
    {
        public const long Scale = 1000;

        public readonly long Raw;

        public Fixed(long raw)
        {
            Raw = raw;
        }

        public static readonly Fixed Zero = new Fixed(0);
        public static readonly Fixed One = new Fixed(Scale);

        public static Fixed FromRaw(long raw) => new Fixed(raw);
        public static Fixed FromInt(long whole) => new Fixed(whole * Scale);
        public static Fixed FromRatio(long numerator, long denominator) => new Fixed(numerator * Scale / denominator);

        public static Fixed operator +(Fixed a, Fixed b) => new Fixed(a.Raw + b.Raw);
        public static Fixed operator -(Fixed a, Fixed b) => new Fixed(a.Raw - b.Raw);
        public static Fixed operator -(Fixed a) => new Fixed(-a.Raw);
        public static Fixed operator *(Fixed a, Fixed b) => new Fixed(a.Raw * b.Raw / Scale);
        public static Fixed operator /(Fixed a, Fixed b) => new Fixed(a.Raw * Scale / b.Raw);

        public static bool operator <(Fixed a, Fixed b) => a.Raw < b.Raw;
        public static bool operator >(Fixed a, Fixed b) => a.Raw > b.Raw;
        public static bool operator <=(Fixed a, Fixed b) => a.Raw <= b.Raw;
        public static bool operator >=(Fixed a, Fixed b) => a.Raw >= b.Raw;
        public static bool operator ==(Fixed a, Fixed b) => a.Raw == b.Raw;
        public static bool operator !=(Fixed a, Fixed b) => a.Raw != b.Raw;

        public static Fixed Abs(Fixed a) => a.Raw < 0 ? new Fixed(-a.Raw) : a;
        public static Fixed Min(Fixed a, Fixed b) => a.Raw < b.Raw ? a : b;
        public static Fixed Max(Fixed a, Fixed b) => a.Raw > b.Raw ? a : b;

        // Integer (Babylonian) square root, kept in the same fixed-point scale. Deterministic
        // because it only uses integer +, -, and truncating division.
        public static Fixed Sqrt(Fixed value)
        {
            if (value.Raw <= 0) return Zero;

            long target = value.Raw * Scale;
            long x = target;
            long y = (x + 1) / 2;
            while (y < x)
            {
                x = y;
                y = (x + target / x) / 2;
            }
            return new Fixed(x);
        }

        public bool Equals(Fixed other) => Raw == other.Raw;
        public override bool Equals(object obj) => obj is Fixed other && Equals(other);
        public override int GetHashCode() => Raw.GetHashCode();
        public int CompareTo(Fixed other) => Raw.CompareTo(other.Raw);

        public override string ToString()
        {
            long whole = Raw / Scale;
            long frac = Math.Abs(Raw % Scale);
            return $"{whole}.{frac:D3}";
        }
    }
}
