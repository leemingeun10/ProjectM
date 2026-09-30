using System.Collections.Generic;
using Game.Sim.Ai;
using Game.Sim.Battle;
using Game.Sim.Stroke;
using Game.Sim.Technique;
using NUnit.Framework;

namespace Game.Tests
{
    public class AiTechniqueGeneratorTests
    {
        private const int SeedCount = 100;

        private static readonly AiDifficulty[] AllDifficulties = { AiDifficulty.Easy, AiDifficulty.Normal, AiDifficulty.Hard };

        private static void AssertSameSteps(IReadOnlyList<TechniqueStep> a, IReadOnlyList<TechniqueStep> b)
        {
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].IsCounter, b[i].IsCounter, $"step {i} isCounter");
                Assert.AreEqual(a[i].Stroke.Type, b[i].Stroke.Type, $"step {i} type");
                Assert.AreEqual(a[i].Stroke.Area.Raw, b[i].Stroke.Area.Raw, $"step {i} area");
                Assert.AreEqual(a[i].Stroke.Length.Raw, b[i].Stroke.Length.Raw, $"step {i} length");
                Assert.AreEqual(a[i].RawPoints.Count, b[i].RawPoints.Count, $"step {i} point count");
                for (int j = 0; j < a[i].RawPoints.Count; j++)
                {
                    Assert.AreEqual(a[i].RawPoints[j].X.Raw, b[i].RawPoints[j].X.Raw, $"step {i} point {j} x");
                    Assert.AreEqual(a[i].RawPoints[j].Y.Raw, b[i].RawPoints[j].Y.Raw, $"step {i} point {j} y");
                }
            }
        }

        [Test]
        public void Generate_ProducesFullOrderedSequence()
        {
            foreach (AiDifficulty difficulty in AllDifficulties)
            {
                List<TechniqueStep> steps = AiTechniqueGenerator.Generate(difficulty, 42);
                Assert.AreEqual(TechniqueBuilder.MaxTurns, steps.Count, difficulty.ToString());
                for (int i = 0; i < steps.Count; i++)
                    Assert.AreEqual(i, steps[i].TurnIndex);
            }
        }

        [Test]
        public void SameSeed_IsBitIdentical()
        {
            foreach (AiDifficulty difficulty in AllDifficulties)
                AssertSameSteps(AiTechniqueGenerator.Generate(difficulty, 777), AiTechniqueGenerator.Generate(difficulty, 777));
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentTechniques()
        {
            List<TechniqueStep> a = AiTechniqueGenerator.Generate(AiDifficulty.Normal, 1);
            List<TechniqueStep> b = AiTechniqueGenerator.Generate(AiDifficulty.Normal, 2);
            Assert.AreNotEqual(a[0].RawPoints[0].X.Raw, b[0].RawPoints[0].X.Raw);
        }

        [Test]
        public void CounterCount_MatchesProfile()
        {
            foreach (AiDifficulty difficulty in AllDifficulties)
            {
                int expected = AiProfile.For(difficulty).CounterCount;
                for (ulong seed = 0; seed < SeedCount; seed++)
                {
                    int counters = 0;
                    foreach (TechniqueStep step in AiTechniqueGenerator.Generate(difficulty, seed))
                        if (step.IsCounter) counters++;
                    Assert.AreEqual(expected, counters, $"{difficulty} seed {seed}");
                }
            }
        }

        [Test]
        public void AllPoints_StayInsideDrawingArea()
        {
            long maxX = AiTechniqueGenerator.HalfWidth * Fixed.Scale;
            long maxY = AiTechniqueGenerator.HalfHeight * Fixed.Scale;
            foreach (AiDifficulty difficulty in AllDifficulties)
            {
                for (ulong seed = 0; seed < SeedCount; seed++)
                {
                    foreach (TechniqueStep step in AiTechniqueGenerator.Generate(difficulty, seed))
                    {
                        foreach (StrokePoint p in step.RawPoints)
                        {
                            Assert.LessOrEqual(System.Math.Abs(p.X.Raw), maxX, $"{difficulty} seed {seed} turn {step.TurnIndex}");
                            Assert.LessOrEqual(System.Math.Abs(p.Y.Raw), maxY, $"{difficulty} seed {seed} turn {step.TurnIndex}");
                        }
                    }
                }
            }
        }

        [Test]
        public void StoredStroke_MatchesAnalyzerOnStoredPoints()
        {
            // The stroke classification must be exactly what a player drawing these points would get.
            foreach (AiDifficulty difficulty in AllDifficulties)
            {
                for (ulong seed = 0; seed < SeedCount; seed++)
                {
                    foreach (TechniqueStep step in AiTechniqueGenerator.Generate(difficulty, seed))
                    {
                        StrokeResult analyzed = StrokeAnalyzer.Analyze(step.RawPoints);
                        Assert.AreEqual(analyzed.Type, step.Stroke.Type);
                        Assert.AreEqual(analyzed.Area.Raw, step.Stroke.Area.Raw);
                        Assert.AreEqual(analyzed.Length.Raw, step.Stroke.Length.Raw);
                    }
                }
            }
        }

        [Test]
        public void ThrustPercentZero_ProducesNoThrusts_AndHundredOnlyThrustsOrCounters()
        {
            var noThrust = new AiProfile(0, 200, 300, 60, 80, 2);
            var allThrust = new AiProfile(100, 200, 300, 60, 80, 2);
            for (ulong seed = 0; seed < 20; seed++)
            {
                foreach (TechniqueStep step in AiTechniqueGenerator.Generate(noThrust, seed))
                    Assert.AreNotEqual(BattleStepType.Thrust, BattleSimulator.GetStepType(step));
                foreach (TechniqueStep step in AiTechniqueGenerator.Generate(allThrust, seed))
                    Assert.AreNotEqual(BattleStepType.Slash, BattleSimulator.GetStepType(step));
            }
        }

        [Test]
        public void HarderDifficulty_HasHigherTotalThrustDamage()
        {
            long easy = 0, hard = 0;
            for (ulong seed = 0; seed < SeedCount; seed++)
            {
                easy += TotalThrustDamage(AiTechniqueGenerator.Generate(AiDifficulty.Easy, seed));
                hard += TotalThrustDamage(AiTechniqueGenerator.Generate(AiDifficulty.Hard, seed));
            }
            Assert.Greater(hard, easy);
        }

        private static long TotalThrustDamage(List<TechniqueStep> steps)
        {
            long total = 0;
            foreach (TechniqueStep step in steps)
                if (BattleSimulator.GetStepType(step) == BattleStepType.Thrust)
                    total += BattleSimulator.ThrustDamage(step);
            return total;
        }

        [Test]
        public void GeneratedTechnique_IsBattleReadyAndDeterministic()
        {
            List<TechniqueStep> a = AiTechniqueGenerator.Generate(AiDifficulty.Hard, 10);
            List<TechniqueStep> b = AiTechniqueGenerator.Generate(AiDifficulty.Easy, 20);

            BattleResult first = BattleSimulator.Simulate(a, b);
            BattleResult second = BattleSimulator.Simulate(
                AiTechniqueGenerator.Generate(AiDifficulty.Hard, 10), AiTechniqueGenerator.Generate(AiDifficulty.Easy, 20));

            Assert.AreEqual(first.Outcome, second.Outcome);
            Assert.AreEqual(first.Events.Count, second.Events.Count);
            for (int i = 0; i < first.Events.Count; i++)
                Assert.AreEqual(first.Events[i].ToString(), second.Events[i].ToString());
        }

        [Test]
        public void GeneratedTechnique_SurvivesJsonRoundTrip()
        {
            List<TechniqueStep> steps = AiTechniqueGenerator.Generate(AiDifficulty.Normal, 5);
            var builder = new TechniqueBuilder();
            foreach (TechniqueStep step in steps)
                Assert.AreEqual(TechniqueAddResult.Success, builder.TryAddStep(step.IsCounter, step.Stroke, step.RawPoints));

            (_, List<TechniqueStep> restored) = TechniqueJsonSerializer.Deserialize(TechniqueJsonSerializer.Serialize(builder));
            AssertSameSteps(steps, restored);
        }
    }
}
