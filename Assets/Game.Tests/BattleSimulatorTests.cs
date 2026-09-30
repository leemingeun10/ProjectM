using System.Collections.Generic;
using Game.Sim.Battle;
using Game.Sim.Stroke;
using Game.Sim.Technique;
using NUnit.Framework;

namespace Game.Tests
{
    public class BattleSimulatorTests
    {
        // ---- Step fixtures (coordinates in whole sim units) ----

        // Horizontal slash along y = 0 from x = 0 to 200.
        private static readonly StrokePoint[] HorizontalSlash =
            { StrokePoint.FromInt(0, 0), StrokePoint.FromInt(100, 0), StrokePoint.FromInt(200, 0) };

        // Vertical slash through x = 100 that crosses HorizontalSlash and the square's interior line.
        private static readonly StrokePoint[] CrossingSlash =
            { StrokePoint.FromInt(100, -100), StrokePoint.FromInt(100, 100) };

        // Horizontal slash far above everything else: never in contact.
        private static readonly StrokePoint[] FarSlash =
            { StrokePoint.FromInt(0, 1000), StrokePoint.FromInt(200, 1000) };

        // Closed 200x200 square loop (area 40000).
        private static readonly StrokePoint[] SquareLoop =
        {
            StrokePoint.FromInt(0, 0), StrokePoint.FromInt(200, 0), StrokePoint.FromInt(200, 200),
            StrokePoint.FromInt(0, 200), StrokePoint.FromInt(0, 0)
        };

        // Slash cutting straight through SquareLoop at y = 100.
        private static readonly StrokePoint[] SlashThroughSquare =
            { StrokePoint.FromInt(-50, 100), StrokePoint.FromInt(250, 100) };

        private enum Kind { Slash, Thrust, Counter }

        private readonly struct Spec
        {
            public readonly Kind Kind;
            public readonly StrokePoint[] Points;
            public readonly long Area;

            public Spec(Kind kind, StrokePoint[] points, long area = 0)
            {
                Kind = kind;
                Points = points;
                Area = area;
            }
        }

        private static Spec Slash(StrokePoint[] points) => new Spec(Kind.Slash, points);
        private static Spec Thrust(long area, StrokePoint[] points = null) => new Spec(Kind.Thrust, points ?? SquareLoop, area);
        private static Spec Counter(StrokePoint[] points) => new Spec(Kind.Counter, points);

        // Area that deals exactly `damage`, so these tests don't hard-code the divisor value.
        private static long AreaFor(int damage) => (long)damage * BattleConstants.ThrustDamageDivisor;

        // Counter vs Counter always deals 0, so it is a neutral filler for the unused turns.
        private static readonly Spec Filler = Counter(FarSlash);

        private static TechniqueStep MakeStep(int turn, Spec spec)
        {
            StrokeResult stroke = spec.Kind == Kind.Thrust
                ? StrokeResult.Thrust(Fixed.FromInt(spec.Area))
                : StrokeResult.Slash(Fixed.FromInt(200), Fixed.One, Fixed.Zero, Fixed.One, Fixed.Zero);
            return new TechniqueStep(turn, spec.Kind == Kind.Counter, stroke, spec.Points);
        }

        // Builds a full 10-step sequence: the given specs fill turns 0.., the rest are Filler.
        private static List<TechniqueStep> Sequence(params Spec[] specs)
        {
            var steps = new List<TechniqueStep>(BattleSimulator.TurnsPerBattle);
            for (int i = 0; i < BattleSimulator.TurnsPerBattle; i++)
                steps.Add(MakeStep(i, i < specs.Length ? specs[i] : Filler));
            return steps;
        }

        private static TurnEvent FirstTurn(Spec a, Spec b)
            => BattleSimulator.Simulate(Sequence(a), Sequence(b)).Events[0];

        private static void AssertTurn(TurnEvent e, BattleStepType typeA, BattleStepType typeB, int damageToA, int damageToB, TurnReason reason)
        {
            Assert.AreEqual(typeA, e.TypeA, "TypeA");
            Assert.AreEqual(typeB, e.TypeB, "TypeB");
            Assert.AreEqual(damageToA, e.DamageToA, "DamageToA");
            Assert.AreEqual(damageToB, e.DamageToB, "DamageToB");
            Assert.AreEqual(reason, e.Reason, "Reason");
            Assert.AreEqual(BattleConstants.StartingHP - damageToA, e.HpAAfter, "HpAAfter");
            Assert.AreEqual(BattleConstants.StartingHP - damageToB, e.HpBAfter, "HpBAfter");
        }

        // ---- Per-matchup rules ----

        [Test]
        public void SlashVsSlash_Crossing_BothTakeZero()
        {
            AssertTurn(FirstTurn(Slash(HorizontalSlash), Slash(CrossingSlash)),
                BattleStepType.Slash, BattleStepType.Slash, 0, 0, TurnReason.CrossContact);
        }

        [Test]
        public void SlashVsSlash_NotCrossing_BothTakeSlashDamage()
        {
            AssertTurn(FirstTurn(Slash(HorizontalSlash), Slash(FarSlash)),
                BattleStepType.Slash, BattleStepType.Slash, 10, 10, TurnReason.Miss);
        }

        [Test]
        public void CounterVsSlash_Crossing_SlashTakesCounterBonus()
        {
            AssertTurn(FirstTurn(Counter(CrossingSlash), Slash(HorizontalSlash)),
                BattleStepType.Counter, BattleStepType.Slash, 0, 20, TurnReason.CrossContact);

            // Mirrored sides.
            AssertTurn(FirstTurn(Slash(HorizontalSlash), Counter(CrossingSlash)),
                BattleStepType.Slash, BattleStepType.Counter, 20, 0, TurnReason.CrossContact);
        }

        [Test]
        public void CounterVsSlash_NotCrossing_CounterTakesSlashDamage()
        {
            AssertTurn(FirstTurn(Counter(FarSlash), Slash(HorizontalSlash)),
                BattleStepType.Counter, BattleStepType.Slash, 10, 0, TurnReason.Miss);

            AssertTurn(FirstTurn(Slash(HorizontalSlash), Counter(FarSlash)),
                BattleStepType.Slash, BattleStepType.Counter, 0, 10, TurnReason.Miss);
        }

        [Test]
        public void CounterStep_IgnoresItsStrokeType()
        {
            // A counter drawn as a loop is still a Counter, not a Thrust.
            var counterAsLoop = new TechniqueStep(0, true, StrokeResult.Thrust(Fixed.FromInt(90000)), SquareLoop);
            Assert.AreEqual(BattleStepType.Counter, BattleSimulator.GetStepType(counterAsLoop));
        }

        [Test]
        public void ThrustVsSlash_Intercepted_ThrustTakesOnlySlashDamage()
        {
            AssertTurn(FirstTurn(Thrust(AreaFor(40)), Slash(SlashThroughSquare)),
                BattleStepType.Thrust, BattleStepType.Slash, 10, 0, TurnReason.CrossContact);

            AssertTurn(FirstTurn(Slash(SlashThroughSquare), Thrust(AreaFor(40))),
                BattleStepType.Slash, BattleStepType.Thrust, 0, 10, TurnReason.CrossContact);
        }

        [Test]
        public void ThrustVsSlash_NotIntercepted_SlashTakesAreaDamage()
        {
            AssertTurn(FirstTurn(Thrust(AreaFor(40)), Slash(FarSlash)),
                BattleStepType.Thrust, BattleStepType.Slash, 0, 40, TurnReason.Miss);

            AssertTurn(FirstTurn(Slash(FarSlash), Thrust(AreaFor(40))),
                BattleStepType.Slash, BattleStepType.Thrust, 40, 0, TurnReason.Miss);
        }

        [Test]
        public void ThrustVsThrust_EachTakesTheOthersAreaDamage()
        {
            // A deals 40 to B. B's area is one unit short of 26 damage, so it truncates to 25.
            AssertTurn(FirstTurn(Thrust(AreaFor(40)), Thrust(AreaFor(26) - 1)),
                BattleStepType.Thrust, BattleStepType.Thrust, 25, 40, TurnReason.Unconditional);
        }

        [Test]
        public void ThrustVsCounter_CounterAlwaysTakesAreaDamage()
        {
            // Even a counter drawn right through the thrust loop cannot stop it.
            AssertTurn(FirstTurn(Thrust(AreaFor(30)), Counter(SlashThroughSquare)),
                BattleStepType.Thrust, BattleStepType.Counter, 0, 30, TurnReason.Unconditional);

            AssertTurn(FirstTurn(Counter(FarSlash), Thrust(AreaFor(30))),
                BattleStepType.Counter, BattleStepType.Thrust, 30, 0, TurnReason.Unconditional);
        }

        [Test]
        public void ThrustDamage_Radius150LoopMatchesSlashDamage()
        {
            // Calibration anchor: a radius-150 loop sampled like the client does (24 points, closed)
            // must deal the same as a basic slash.
            int[] cos24 = { 1000, 966, 866, 707, 500, 259, 0, -259, -500, -707, -866, -966,
                            -1000, -966, -866, -707, -500, -259, 0, 259, 500, 707, 866, 966 };
            var loop = new StrokePoint[25];
            for (int k = 0; k < 24; k++)
                loop[k] = new StrokePoint(cos24[k] * 150L, cos24[(k + 18) % 24] * 150L);
            loop[24] = loop[0];

            StrokeResult stroke = StrokeAnalyzer.Analyze(loop);
            Assert.AreEqual(StrokeType.Thrust, stroke.Type);
            Assert.AreEqual(BattleConstants.SlashDamage, BattleSimulator.ThrustDamage(new TechniqueStep(0, false, stroke, loop)));
        }

        [Test]
        public void CounterVsCounter_BothTakeZero()
        {
            AssertTurn(FirstTurn(Counter(HorizontalSlash), Counter(CrossingSlash)),
                BattleStepType.Counter, BattleStepType.Counter, 0, 0, TurnReason.Unconditional);
        }

        // ---- Battle flow ----

        [Test]
        public void KnockoutMidBattle_StopsImmediately()
        {
            // 60 damage per turn to B: HP 100 -> 40 -> -20, so the battle ends on turn 1.
            List<TechniqueStep> a = Sequence(Thrust(AreaFor(60)), Thrust(AreaFor(60)), Thrust(AreaFor(60)));
            List<TechniqueStep> b = Sequence(Counter(FarSlash), Counter(FarSlash), Counter(FarSlash));

            BattleResult result = BattleSimulator.Simulate(a, b);

            Assert.AreEqual(2, result.Events.Count);
            Assert.AreEqual(1, result.Events[result.Events.Count - 1].TurnIndex);
            Assert.AreEqual(100, result.FinalHpA);
            Assert.AreEqual(-20, result.FinalHpB);
            Assert.AreEqual(BattleOutcome.WinA, result.Outcome);
        }

        [Test]
        public void FullBattle_HigherHpWins()
        {
            // Turn 0: slash trade (both -10). Turn 1: A's counter lands (B -20). A 90 vs B 70.
            List<TechniqueStep> a = Sequence(Slash(HorizontalSlash), Counter(CrossingSlash));
            List<TechniqueStep> b = Sequence(Slash(FarSlash), Slash(HorizontalSlash));

            BattleResult result = BattleSimulator.Simulate(a, b);
            Assert.AreEqual(BattleSimulator.TurnsPerBattle, result.Events.Count);
            Assert.AreEqual(90, result.FinalHpA);
            Assert.AreEqual(70, result.FinalHpB);
            Assert.AreEqual(BattleOutcome.WinA, result.Outcome);

            BattleResult mirrored = BattleSimulator.Simulate(b, a);
            Assert.AreEqual(BattleOutcome.WinB, mirrored.Outcome);
        }

        [Test]
        public void FullBattle_EqualHpIsDraw()
        {
            List<TechniqueStep> a = Sequence(Slash(HorizontalSlash));
            List<TechniqueStep> b = Sequence(Slash(FarSlash));

            BattleResult result = BattleSimulator.Simulate(a, b);

            Assert.AreEqual(BattleSimulator.TurnsPerBattle, result.Events.Count);
            Assert.AreEqual(90, result.FinalHpA);
            Assert.AreEqual(90, result.FinalHpB);
            Assert.AreEqual(BattleOutcome.Draw, result.Outcome);
        }

        [Test]
        public void SameInputs_ProduceIdenticalResultsEveryRun()
        {
            List<TechniqueStep> a = Sequence(
                Slash(HorizontalSlash), Thrust(12345), Counter(CrossingSlash), Slash(SlashThroughSquare),
                Thrust(8000), Counter(FarSlash), Slash(FarSlash), Thrust(15500), Slash(CrossingSlash), Counter(HorizontalSlash));
            List<TechniqueStep> b = Sequence(
                Slash(CrossingSlash), Slash(FarSlash), Slash(HorizontalSlash), Thrust(9000),
                Thrust(7000), Slash(HorizontalSlash), Counter(HorizontalSlash), Counter(SlashThroughSquare), Slash(FarSlash), Thrust(4000));

            BattleResult first = BattleSimulator.Simulate(a, b);
            for (int run = 0; run < 5; run++)
            {
                BattleResult again = BattleSimulator.Simulate(a, b);
                CollectionAssert.AreEqual(first.Events, again.Events, $"run {run} events");
                Assert.AreEqual(first.FinalHpA, again.FinalHpA, $"run {run} FinalHpA");
                Assert.AreEqual(first.FinalHpB, again.FinalHpB, $"run {run} FinalHpB");
                Assert.AreEqual(first.Outcome, again.Outcome, $"run {run} Outcome");
            }
        }

        [Test]
        public void SequenceWithWrongLength_Throws()
        {
            var shortSequence = Sequence().GetRange(0, 9);
            Assert.Throws<System.ArgumentException>(() => BattleSimulator.Simulate(shortSequence, Sequence()));
        }
    }
}
