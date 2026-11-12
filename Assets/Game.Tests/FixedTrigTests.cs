using System;
using Game.Sim.Common;
using NUnit.Framework;

namespace Game.Tests
{
    public class FixedTrigTests
    {
        [TestCase(0, 0, 10000)]
        [TestCase(30, 5000, 8660)]
        [TestCase(90, 10000, 0)]
        [TestCase(180, 0, -10000)]
        [TestCase(270, -10000, 0)]
        [TestCase(210, -5000, -8660)]
        [TestCase(-90, -10000, 0)]
        [TestCase(450, 10000, 0)]
        public void SinCos_KnownAngles(int degrees, int sin, int cos)
        {
            Assert.AreEqual(sin, FixedTrig.Sin(degrees));
            Assert.AreEqual(cos, FixedTrig.Cos(degrees));
        }

        [Test]
        public void SinCos_MatchMathWithinRoundingForEveryDegree()
        {
            for (int d = 0; d < 360; d++)
            {
                double radians = d * Math.PI / 180;
                Assert.AreEqual(Math.Sin(radians) * FixedTrig.Scale, FixedTrig.Sin(d), 0.51, $"sin {d}");
                Assert.AreEqual(Math.Cos(radians) * FixedTrig.Scale, FixedTrig.Cos(d), 0.51, $"cos {d}");
            }
        }

        [Test]
        public void Cos_ExtremeAngle_DoesNotOverflow()
        {
            Assert.AreEqual(FixedTrig.Cos(int.MaxValue % 360), FixedTrig.Cos(int.MaxValue));
        }
    }
}
