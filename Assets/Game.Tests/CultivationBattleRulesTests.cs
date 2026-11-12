using Game.Sim.Cultivation;
using NUnit.Framework;

namespace Game.Tests
{
    public class CultivationBattleRulesTests
    {
        // ---- MaxStrokeLength ----

        [TestCase(0, 10)]
        [TestCase(100, 12)]
        [TestCase(49, 10)]
        [TestCase(50, 11)]
        [TestCase(99, 11)]
        public void MaxStrokeLength_IsLinearBetweenZeroAndHundred(int externalPower, int expected)
        {
            Assert.AreEqual(expected, CultivationBattleRules.MaxStrokeLength(externalPower));
        }

        [TestCase(-10, 10)]
        [TestCase(150, 12)]
        [TestCase(int.MaxValue, 12)]
        public void MaxStrokeLength_IsClampedOutsideZeroToHundred(int externalPower, int expected)
        {
            Assert.AreEqual(expected, CultivationBattleRules.MaxStrokeLength(externalPower));
        }

        [Test]
        public void MaxStrokeLength_NeverDecreasesAsPowerGrows()
        {
            for (int power = 1; power <= 200; power++)
                Assert.GreaterOrEqual(CultivationBattleRules.MaxStrokeLength(power), CultivationBattleRules.MaxStrokeLength(power - 1), $"power {power}");
        }
    }
}
