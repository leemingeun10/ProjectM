using System;

namespace Game.Sim.Common
{
    public static class IntegerMath
    {
        // floor(sqrt(value)) via integer Newton's method. Only integer +, / and comparisons, so the
        // result is identical on every device. Starting at value / 2 + 1 (>= sqrt for value >= 2)
        // keeps x + value / x from overflowing even at long.MaxValue.
        public static long IntegerSqrt(long value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Square root of a negative number.");
            if (value < 2)
                return value;

            long x = value / 2 + 1;
            long y = (x + value / x) / 2;
            while (y < x)
            {
                x = y;
                y = (x + value / x) / 2;
            }
            return x;
        }
    }
}
