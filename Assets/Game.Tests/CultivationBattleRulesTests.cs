using System;
using System.Collections.Generic;
using Game.Sim.Cultivation;
using Game.Sim.Shape;
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

        // ---- TryActivateHiddenTechnique ----

        private static readonly ShapeMatchResult TriangleMatch = new ShapeMatchResult(ShapeTemplates.TriangleId, 9500);

        [Test]
        public void TryActivate_NotEnoughInternalPower_FailsAndKeepsPower()
        {
            (bool activated, int remaining) = CultivationBattleRules.TryActivateHiddenTechnique(29, TriangleMatch, CultivationBattleRules.HiddenTechniqueCosts);

            Assert.IsFalse(activated);
            Assert.AreEqual(29, remaining);
        }

        [TestCase(30, 0)]
        [TestCase(75, 45)]
        public void TryActivate_EnoughInternalPower_SpendsTheCost(int power, int expectedRemaining)
        {
            (bool activated, int remaining) = CultivationBattleRules.TryActivateHiddenTechnique(power, TriangleMatch, CultivationBattleRules.HiddenTechniqueCosts);

            Assert.IsTrue(activated);
            Assert.AreEqual(expectedRemaining, remaining);
        }

        [Test]
        public void TryActivate_UsesTheCostOfTheMatchedTemplate()
        {
            var costs = new Dictionary<string, int> { { "triangle", 10 }, { "flower", 50 } };

            Assert.AreEqual((true, 30), CultivationBattleRules.TryActivateHiddenTechnique(40, new ShapeMatchResult("triangle", 9100), costs));
            Assert.AreEqual((false, 40), CultivationBattleRules.TryActivateHiddenTechnique(40, new ShapeMatchResult("flower", 9100), costs));
        }

        [Test]
        public void TryActivate_UnknownTemplate_Fails()
        {
            Assert.AreEqual((false, 100), CultivationBattleRules.TryActivateHiddenTechnique(100, new ShapeMatchResult("square", 9900), CultivationBattleRules.HiddenTechniqueCosts));
            Assert.AreEqual((false, 100), CultivationBattleRules.TryActivateHiddenTechnique(100, default, CultivationBattleRules.HiddenTechniqueCosts));
        }

        [Test]
        public void TryActivate_NullCosts_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => CultivationBattleRules.TryActivateHiddenTechnique(100, TriangleMatch, null));
        }

        [Test]
        public void HiddenTechniqueCosts_CoverEveryBuiltInTemplateAt30()
        {
            foreach (ShapeTemplate template in ShapeTemplates.All)
                Assert.AreEqual(30, CultivationBattleRules.HiddenTechniqueCosts[template.Id], template.Id);
        }

        [Test]
        public void HiddenTechniqueCosts_CoverEveryTemplate()
        {
            foreach (ShapeTemplate template in ShapeTemplates.All)
                Assert.IsTrue(CultivationBattleRules.HiddenTechniqueCosts.ContainsKey(template.Id), template.Id);
        }
    }
}
