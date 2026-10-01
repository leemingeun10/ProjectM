using System;
using Game.Sim.Common;
using NUnit.Framework;

namespace Game.Tests
{
    public class IntegerMathTests
    {
        [TestCase(0L, 0L)]
        [TestCase(1L, 1L)]
        [TestCase(2L, 1L)]
        [TestCase(3L, 1L)]
        [TestCase(4L, 2L)]
        [TestCase(99L, 9L)]
        [TestCase(100L, 10L)]
        [TestCase(10000L, 100L)]
        [TestCase(69882L, 264L)]   // client's 24-point radius-150 loop (264^2 = 69,696, 265^2 = 70,225)
        [TestCase(70686L, 265L)]   // perfect radius-150 circle
        [TestCase(long.MaxValue, 3037000499L)]
        public void IntegerSqrt_KnownValues(long value, long expected)
        {
            Assert.AreEqual(expected, IntegerMath.IntegerSqrt(value));
        }

        [Test]
        public void IntegerSqrt_IsFloorAroundEveryPerfectSquare()
        {
            for (long r = 1; r <= 2000; r++)
            {
                Assert.AreEqual(r, IntegerMath.IntegerSqrt(r * r), $"{r}^2");
                Assert.AreEqual(r - 1, IntegerMath.IntegerSqrt(r * r - 1), $"{r}^2 - 1");
                Assert.AreEqual(r, IntegerMath.IntegerSqrt(r * r + 2 * r), $"({r}+1)^2 - 1");
            }
        }

        [Test]
        public void IntegerSqrt_Negative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => IntegerMath.IntegerSqrt(-1));
        }
    }
}
