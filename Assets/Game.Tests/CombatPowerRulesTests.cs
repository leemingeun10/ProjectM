using System;
using System.Collections.Generic;
using Game.Sim.Ai;
using Game.Sim.Battle;
using Game.Sim.Cultivation;
using Game.Sim.Shape;
using Game.Sim.Stroke;
using Game.Sim.Technique;
using NUnit.Framework;

namespace Game.Tests
{
    // 전투력: the stat rules (CombatPowerRules) and how BattleSimulator applies them.
    public class CombatPowerRulesTests
    {
        // ---- Fixtures (whole sim units) ----

        private static readonly StrokePoint[] HorizontalSlash =
            { StrokePoint.FromInt(0, 0), StrokePoint.FromInt(100, 0), StrokePoint.FromInt(200, 0) };
        private static readonly StrokePoint[] CrossingSlash =
            { StrokePoint.FromInt(100, -100), StrokePoint.FromInt(100, 100) };
        private static readonly StrokePoint[] FarSlash =
            { StrokePoint.FromInt(0, 5000), StrokePoint.FromInt(200, 5000) };
        // Closed right triangle, area 72000 -> sqrt 268 -> base thrust damage 10.
        private static readonly StrokePoint[] Triangle =
        {
            StrokePoint.FromInt(0, 0), StrokePoint.FromInt(400, 0), StrokePoint.FromInt(0, 360), StrokePoint.FromInt(0, 0)
        };

        private static TechniqueStep Slash(StrokePoint[] points) => new TechniqueStep(0, false, StrokeAnalyzer.Analyze(points), points);
        private static TechniqueStep Counter(StrokePoint[] points) => new TechniqueStep(0, true, StrokeAnalyzer.Analyze(points), points);
        private static TechniqueStep Thrust() => new TechniqueStep(0, false, StrokeAnalyzer.Analyze(Triangle), Triangle);
        private static TechniqueStep HiddenSlash()
            => new TechniqueStep(0, false, StrokeAnalyzer.Analyze(Triangle), Triangle, ShapeTemplates.TriangleId, HiddenTechniqueMove.HorizontalSlash);

        // Counter vs Counter deals nothing, so it fills the turns a test does not use.
        private static List<TechniqueStep> Sequence(params TechniqueStep[] first)
        {
            var steps = new List<TechniqueStep>();
            for (int i = 0; i < BattleSimulator.TurnsPerBattle; i++)
                steps.Add((i < first.Length ? first[i] : Counter(FarSlash)).WithTurnIndex(i));
            return steps;
        }

        private static List<TechniqueStep> Repeat(TechniqueStep step, int turns)
        {
            var first = new TechniqueStep[turns];
            for (int i = 0; i < turns; i++)
                first[i] = step;
            return Sequence(first);
        }

        private static CombatStats Stats(int internalPower, int externalPower) => new CombatStats(internalPower, externalPower);

        // A plays slash, counter, thrust and hidden slash; all four land on B.
        // Turn 0: slash trade. Turn 1: A counters a slash. Turn 2: A thrusts into a counter.
        // Turn 3: hidden slash (105%) trades with a slash.
        private static List<TechniqueStep> EveryMoveA() => Sequence(Slash(HorizontalSlash), Counter(CrossingSlash), Thrust(), HiddenSlash());
        private static List<TechniqueStep> EveryMoveB() => Sequence(Slash(FarSlash), Slash(HorizontalSlash), Counter(FarSlash), Slash(FarSlash));

        // ---- Combat power ----

        [TestCase(100, 100, 1000)]
        [TestCase(50, 200, 1000)]
        [TestCase(200, 50, 1000)]
        [TestCase(200, 200, 4000)]
        [TestCase(75, 75, 563)]     // 0.5625 rounds half up
        [TestCase(110, 100, 1100)]
        [TestCase(1, 1, 0)]         // 0.0001
        public void PowerPermille_IsInternalTimesExternalOverTenThousand(int internalPower, int externalPower, int expected)
            => Assert.AreEqual(expected, CombatPowerRules.PowerPermille(Stats(internalPower, externalPower)));

        [Test]
        public void FromCultivation_AddsTheTrainedValuesToOneHundred()
        {
            CombatStats fresh = CombatPowerRules.FromCultivation(default);
            Assert.AreEqual(100, fresh.InternalPower);
            Assert.AreEqual(100, fresh.ExternalPower);
            Assert.AreEqual(1000, CombatPowerRules.PowerPermille(fresh));

            // CultivationStats(external, internal, date)
            CombatStats trained = CombatPowerRules.FromCultivation(new CultivationStats(30, 50, 20261120));
            Assert.AreEqual(150, trained.InternalPower);
            Assert.AreEqual(130, trained.ExternalPower);
            Assert.AreEqual(1950, CombatPowerRules.PowerPermille(trained));
        }

        [Test]
        public void DefaultStats_AreOneHundredEach()
        {
            Assert.AreEqual(100, CombatStats.Default.InternalPower);
            Assert.AreEqual(100, CombatStats.Default.ExternalPower);
            Assert.AreEqual(BattleConstants.StartingHP, CombatPowerRules.MaxHp(CombatStats.Default));
        }

        [TestCase(0, 100)]
        [TestCase(100, 0)]
        [TestCase(-5, 100)]
        public void Stats_MustBePositive(int internalPower, int externalPower)
            => Assert.Throws<ArgumentOutOfRangeException>(() => Stats(internalPower, externalPower));

        // ---- Damage formula ----

        [Test]
        public void Damage_AtExternalOneHundred_IsTheOldDamage()
        {
            Assert.AreEqual(1000, CombatPowerRules.DamageScalePermille, "x is 1 for now");
            for (int baseDamage = 0; baseDamage <= 200; baseDamage++)
            {
                Assert.AreEqual(baseDamage, CombatPowerRules.Damage(baseDamage, 100, 100), $"ordinary {baseDamage}");
                Assert.AreEqual(HiddenTechniques.ScaleDamage(baseDamage, 105), CombatPowerRules.Damage(baseDamage, 105, 100), $"hidden {baseDamage}");
            }
        }

        [TestCase(10, 100, 200, 20)]
        [TestCase(10, 100, 50, 5)]
        [TestCase(10, 100, 75, 8)]    // 7.5 rounds half up
        [TestCase(10, 100, 74, 7)]    // 7.4
        [TestCase(20, 100, 75, 15)]
        [TestCase(10, 105, 130, 14)]  // 13.65
        [TestCase(10, 105, 110, 12)]  // 11.55
        [TestCase(10, 105, 90, 9)]    // 9.45; rounding the bonus first would give round(11 * 0.9) = 10
        [TestCase(0, 105, 300, 0)]
        public void Damage_MultipliesFirstAndRoundsHalfUpOnce(int baseDamage, int hiddenPercent, int external, int expected)
            => Assert.AreEqual(expected, CombatPowerRules.Damage(baseDamage, hiddenPercent, external));

        // ---- BattleSimulator with stats ----

        [Test]
        public void Simulate_WithoutStats_EqualsOneHundredEach()
        {
            for (int i = 0; i < 40; i++)
            {
                List<TechniqueStep> a = AiTechniqueGenerator.Generate((AiDifficulty)(i % 3), 0xA000UL + (ulong)i);
                List<TechniqueStep> b = AiTechniqueGenerator.Generate((AiDifficulty)((i + 1) % 3), 0xB000UL + (ulong)i);
                BattleResult plain = BattleSimulator.Simulate(a, b);
                BattleResult withStats = BattleSimulator.Simulate(a, b, Stats(100, 100), Stats(100, 100));
                CollectionAssert.AreEqual(plain.Events, withStats.Events, $"pair {i}");
                Assert.AreEqual(plain.FinalHpA, withStats.FinalHpA);
                Assert.AreEqual(plain.FinalHpB, withStats.FinalHpB);
                Assert.AreEqual(plain.Outcome, withStats.Outcome);
                Assert.AreEqual(100, plain.MaxHpA);
                Assert.AreEqual(100, plain.MaxHpB);
            }
        }

        // The numbers the simulator produced before stats existed (HP 100, table damage), written
        // out so a change to the 100 / 100 path cannot hide behind the overload.
        [Test]
        public void Simulate_AtOneHundredEach_GivesThePreStatNumbers()
        {
            BattleResult result = BattleSimulator.Simulate(EveryMoveA(), EveryMoveB());

            Assert.AreEqual(10, result.Events[0].DamageToA);
            Assert.AreEqual(10, result.Events[0].DamageToB);
            Assert.AreEqual(20, result.Events[1].DamageToB);
            Assert.AreEqual(10, result.Events[2].DamageToB);
            Assert.AreEqual(10, result.Events[3].DamageToA);
            Assert.AreEqual(11, result.Events[3].DamageToB);
            Assert.AreEqual(80, result.FinalHpA);
            Assert.AreEqual(49, result.FinalHpB);
            Assert.AreEqual(BattleOutcome.WinA, result.Outcome);
        }

        [Test]
        public void DoubleExternalPower_DoublesEveryKindOfDamage()
        {
            // HP 1000 so nobody is knocked out early.
            BattleResult normal = BattleSimulator.Simulate(EveryMoveA(), EveryMoveB(), Stats(1000, 100), Stats(1000, 100));
            BattleResult strongA = BattleSimulator.Simulate(EveryMoveA(), EveryMoveB(), Stats(1000, 200), Stats(1000, 100));

            for (int turn = 0; turn < 4; turn++)
                Assert.AreEqual(normal.Events[turn].DamageToA, strongA.Events[turn].DamageToA, $"turn {turn}: what B deals is unchanged");
            Assert.AreEqual(20, strongA.Events[0].DamageToB);   // slash 10
            Assert.AreEqual(40, strongA.Events[1].DamageToB);   // counter 20
            Assert.AreEqual(20, strongA.Events[2].DamageToB);   // thrust 10
            Assert.AreEqual(21, strongA.Events[3].DamageToB);   // hidden slash 10 * 105% * 2
        }

        [Test]
        public void DamageUsesTheAttackersExternalPower()
        {
            List<TechniqueStep> a = Sequence(Slash(HorizontalSlash));
            List<TechniqueStep> b = Sequence(Slash(FarSlash));

            BattleResult result = BattleSimulator.Simulate(a, b, Stats(100, 150), Stats(100, 50));

            Assert.AreEqual(5, result.Events[0].DamageToA);
            Assert.AreEqual(15, result.Events[0].DamageToB);
        }

        [Test]
        public void DoubleInternalPower_DoublesMaxHp_AndChangesNoDamage()
        {
            List<TechniqueStep> a = Sequence(Slash(HorizontalSlash));
            List<TechniqueStep> b = Sequence(Slash(FarSlash));

            BattleResult normal = BattleSimulator.Simulate(a, b, Stats(100, 100), Stats(100, 100));
            BattleResult tank = BattleSimulator.Simulate(a, b, Stats(200, 100), Stats(100, 100));

            Assert.AreEqual(100, normal.MaxHpA);
            Assert.AreEqual(200, tank.MaxHpA);
            Assert.AreEqual(100, tank.MaxHpB);
            Assert.AreEqual(normal.Events[0].DamageToA, tank.Events[0].DamageToA);
            Assert.AreEqual(normal.Events[0].DamageToB, tank.Events[0].DamageToB);
            Assert.AreEqual(190, tank.Events[0].HpAAfter);
            Assert.AreEqual(190, tank.FinalHpA);
        }

        [Test]
        public void MoreHpSurvivesAKnockout()
        {
            // 10 slash trades of 10: at 100 HP both fall on the last turn; at 110 A is left standing.
            List<TechniqueStep> a = Repeat(Slash(HorizontalSlash), 10);
            List<TechniqueStep> b = Repeat(Slash(FarSlash), 10);

            Assert.AreEqual(BattleOutcome.Draw, BattleSimulator.Simulate(a, b).Outcome);
            BattleResult result = BattleSimulator.Simulate(a, b, Stats(110, 100), Stats(100, 100));
            Assert.AreEqual(10, result.FinalHpA);
            Assert.AreEqual(0, result.FinalHpB);
            Assert.AreEqual(BattleOutcome.WinA, result.Outcome);
        }

        // ---- Turn-10 verdict by HP share ----

        [Test]
        public void AfterTheLastTurn_TheLargerHpShareWins_NotTheLargerNumber()
        {
            // Five slash trades. A (내공 200, 외공 50) deals 5 a turn, B (내공 50, 외공 240) deals 24.
            // A: 200 - 120 = 80 left, 40% of its HP. B: 50 - 25 = 25 left, 50% of its HP.
            List<TechniqueStep> a = Repeat(Slash(HorizontalSlash), 5);
            List<TechniqueStep> b = Repeat(Slash(FarSlash), 5);

            BattleResult result = BattleSimulator.Simulate(a, b, Stats(200, 50), Stats(50, 240));

            Assert.AreEqual(BattleSimulator.TurnsPerBattle, result.Events.Count);
            Assert.AreEqual(80, result.FinalHpA);
            Assert.AreEqual(25, result.FinalHpB);
            Assert.Greater(result.FinalHpA, result.FinalHpB);
            Assert.AreEqual(BattleOutcome.WinB, result.Outcome, "A has more HP left but a smaller share of its own");
        }

        [Test]
        public void AfterTheLastTurn_TheLargerPoolAlsoWinsWhenItsShareIsLarger()
        {
            // One slash trade of 10 each. A: 190 / 200 = 95%. B: 90 / 100 = 90%.
            BattleResult result = BattleSimulator.Simulate(Sequence(Slash(HorizontalSlash)), Sequence(Slash(FarSlash)),
                Stats(200, 100), Stats(100, 100));
            Assert.AreEqual(BattleOutcome.WinA, result.Outcome);
        }

        [Test]
        public void AfterTheLastTurn_EqualSharesAreADraw()
        {
            // A: 200 HP, takes 20 -> 90%. B: 100 HP, takes 10 -> 90%.
            BattleResult result = BattleSimulator.Simulate(Sequence(Slash(HorizontalSlash)), Sequence(Slash(FarSlash)),
                Stats(200, 100), Stats(100, 200));
            Assert.AreEqual(180, result.FinalHpA);
            Assert.AreEqual(90, result.FinalHpB);
            Assert.AreEqual(BattleOutcome.Draw, result.Outcome);
        }

        [TestCase(90, 100, 180, 200, 0)]
        [TestCase(91, 100, 180, 200, 1)]
        [TestCase(80, 200, 25, 50, -1)]
        [TestCase(1, 3, 33, 100, 1)]        // 0.3333 > 0.33: nothing is rounded in the comparison
        [TestCase(2000000000, 2000000000, 1999999999, 2000000000, 1)]   // no overflow
        public void CompareHpShare_CrossMultiplies(int hpA, int maxA, int hpB, int maxB, int expectedSign)
            => Assert.AreEqual(expectedSign, Math.Sign(CombatPowerRules.CompareHpShare(hpA, maxA, hpB, maxB)));

        [Test]
        public void AKnockout_EndsTheBattleWhateverTheShares()
        {
            // Slash trades of 10: A (50 HP) is down after five turns.
            List<TechniqueStep> a = Repeat(Slash(HorizontalSlash), 10);
            List<TechniqueStep> b = Repeat(Slash(FarSlash), 10);
            BattleResult result = BattleSimulator.Simulate(a, b, Stats(50, 100), Stats(1000, 100));
            Assert.AreEqual(5, result.Events.Count);
            Assert.AreEqual(BattleOutcome.WinB, result.Outcome);
        }

        // ---- Determinism / validation ----

        [Test]
        public void SameInputsWithStats_GiveIdenticalResults()
        {
            List<TechniqueStep> a = AiTechniqueGenerator.Generate(AiDifficulty.Hard, 0x1234UL);
            List<TechniqueStep> b = AiTechniqueGenerator.Generate(AiDifficulty.Normal, 0x5678UL);
            BattleResult first = BattleSimulator.Simulate(a, b, Stats(137, 163), Stats(75, 75));
            for (int run = 0; run < 5; run++)
            {
                BattleResult again = BattleSimulator.Simulate(a, b, Stats(137, 163), Stats(75, 75));
                CollectionAssert.AreEqual(first.Events, again.Events, $"run {run}");
                Assert.AreEqual(first.FinalHpA, again.FinalHpA);
                Assert.AreEqual(first.FinalHpB, again.FinalHpB);
                Assert.AreEqual(first.MaxHpA, again.MaxHpA);
                Assert.AreEqual(first.MaxHpB, again.MaxHpB);
                Assert.AreEqual(first.Outcome, again.Outcome);
            }
            Assert.AreEqual(137, first.MaxHpA);
            Assert.AreEqual(75, first.MaxHpB);
        }

        [Test]
        public void SwappingSides_MirrorsTheResult()
        {
            List<TechniqueStep> a = AiTechniqueGenerator.Generate(AiDifficulty.Hard, 0x1234UL);
            List<TechniqueStep> b = AiTechniqueGenerator.Generate(AiDifficulty.Normal, 0x5678UL);
            BattleResult ab = BattleSimulator.Simulate(a, b, Stats(137, 163), Stats(75, 75));
            BattleResult ba = BattleSimulator.Simulate(b, a, Stats(75, 75), Stats(137, 163));
            Assert.AreEqual(ab.FinalHpA, ba.FinalHpB);
            Assert.AreEqual(ab.FinalHpB, ba.FinalHpA);
            BattleOutcome mirrored = ab.Outcome == BattleOutcome.WinA ? BattleOutcome.WinB
                : ab.Outcome == BattleOutcome.WinB ? BattleOutcome.WinA : BattleOutcome.Draw;
            Assert.AreEqual(mirrored, ba.Outcome);
        }

        [Test]
        public void UninitialisedStats_AreRejected()
        {
            List<TechniqueStep> a = Sequence();
            Assert.Throws<ArgumentException>(() => BattleSimulator.Simulate(a, a, default, CombatStats.Default));
            Assert.Throws<ArgumentException>(() => BattleSimulator.Simulate(a, a, CombatStats.Default, default));
        }
    }
}
