using System;
using Game.Client.Story;
using Game.Sim.Ai;
using Game.Sim.Battle;
using Game.Sim.Progress;
using NUnit.Framework;

namespace Game.Tests
{
    // Recommended combat power per stage (수련 and story) and the enemy stats derived from it.
    public class StageCombatPowerTests
    {
        // ---- Enemy stats from a recommended power ----

        [Test]
        public void EnemyDivisor_IsOnePointEight()
            => Assert.AreEqual(18, CombatPowerRules.EnemyPowerDivisorTenths);

        // stat = round(100 * sqrt(recommended / 1.8)); the 15 stage values, then round numbers.
        [TestCase(1000, 75)]
        [TestCase(1050, 76)]
        [TestCase(1103, 78)]
        [TestCase(1158, 80)]
        [TestCase(1216, 82)]
        [TestCase(1276, 84)]
        [TestCase(1340, 86)]
        [TestCase(1407, 88)]
        [TestCase(1477, 91)]
        [TestCase(1551, 93)]
        [TestCase(1629, 95)]
        [TestCase(1710, 97)]
        [TestCase(1796, 100)]
        [TestCase(1886, 102)]
        [TestCase(1980, 105)]
        [TestCase(1800, 100)]    // exactly 1
        [TestCase(2600, 120)]    // 1.4444 -> 120.19
        [TestCase(4200, 153)]    // 2.3333 -> 152.75
        public void EnemyStats_AreEqualAndFollowTheRecommendedPower(int recommendedPermille, int expected)
        {
            CombatStats stats = CombatPowerRules.EnemyStats(recommendedPermille);
            Assert.AreEqual(expected, stats.InternalPower);
            Assert.AreEqual(expected, stats.ExternalPower);
        }

        [Test]
        public void EnemyStats_RecommendedOne_IsPowerPointFiveSix()
        {
            CombatStats stats = CombatPowerRules.EnemyStats(1000);
            Assert.AreEqual(75, stats.InternalPower);
            Assert.AreEqual(75, stats.ExternalPower);
            Assert.AreEqual(563, CombatPowerRules.PowerPermille(stats));
        }

        [TestCase(1000, 100)]
        [TestCase(4000, 200)]
        [TestCase(250, 50)]
        [TestCase(563, 75)]      // sqrt(5630) = 75.03
        [TestCase(2000, 141)]    // 141.42
        [TestCase(2250, 150)]
        [TestCase(1, 3)]         // sqrt(10) = 3.16
        public void StatsForPower_IsOneHundredTimesTheSquareRoot(int powerPermille, int expected)
        {
            CombatStats stats = CombatPowerRules.StatsForPower(powerPermille);
            Assert.AreEqual(expected, stats.InternalPower);
            Assert.AreEqual(expected, stats.ExternalPower);
        }

        [Test]
        public void EnemyStats_RoundToTheNearestInteger_ForEveryRecommendedPower()
        {
            int divisor = CombatPowerRules.EnemyPowerDivisorTenths;
            for (int rec = 100; rec <= 10000; rec += 7)
            {
                long s = CombatPowerRules.EnemyStats(rec).InternalPower;
                // s is nearest to sqrt(100 * rec / divisor): (2s - 1)^2 <= 4 * 100 * rec / divisor < (2s + 1)^2
                long four = 400L * rec;
                Assert.LessOrEqual((2 * s - 1) * (2 * s - 1) * divisor, four, $"{rec}");
                Assert.Greater((2 * s + 1) * (2 * s + 1) * divisor, four, $"{rec}");
            }
        }

        [Test]
        public void NonPositivePower_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CombatPowerRules.EnemyStats(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CombatPowerRules.StatsForPower(0));
        }

        // ---- 수련 stages ----

        [Test]
        public void TrainingStages_StartAtOneAndRiseFivePercentAStage()
        {
            int[] expected = { 1000, 1050, 1103, 1158, 1216, 1276, 1340, 1407, 1477, 1551, 1629, 1710, 1796, 1886, 1980 };
            Assert.AreEqual(StageCatalog.StageCount, expected.Length);
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], StageCatalog.Stages[i].RecommendedPowerPermille, $"stage {i + 1}");
        }

        // Each value is the previous one * 1.05, to within the rounding of the approved list.
        [Test]
        public void TrainingStages_EachStepIsFivePercent()
        {
            Assert.AreEqual(1000, StageCatalog.Stages[0].RecommendedPowerPermille);
            for (int i = 1; i < StageCatalog.StageCount; i++)
            {
                int previous = StageCatalog.Stages[i - 1].RecommendedPowerPermille;
                int current = StageCatalog.Stages[i].RecommendedPowerPermille;
                Assert.That(current * 100, Is.InRange(previous * 105 - 100, previous * 105 + 100), $"stage {i + 1}");
            }
        }

        // A story battle takes the value of its place among all 15 story stages (story-only
        // stages count), which is the 수련 stage of the same number.
        [Test]
        public void StoryBattle_TakesTheValueOfItsPositionAmongAllStages()
        {
            StoryBook book = StoryDataTests.LoadBook();
            Assert.AreEqual(StageCatalog.StageCount, book.stages.Length);
            for (int i = 0; i < book.stages.Length; i++)
            {
                StoryStage s = book.stages[i];
                if (s.recommendedPowerPermille > 0)
                    Assert.AreEqual(StageCatalog.Stages[i].RecommendedPowerPermille, s.recommendedPowerPermille, s.id);
            }
        }

        [Test]
        public void TrainingStages_RecommendedPowerNeverDecreases()
        {
            for (int i = 0; i < StageCatalog.StageCount; i++)
            {
                Assert.Greater(StageCatalog.Stages[i].RecommendedPowerPermille, 0, $"stage {i + 1}");
                if (i > 0)
                    Assert.GreaterOrEqual(StageCatalog.Stages[i].RecommendedPowerPermille, StageCatalog.Stages[i - 1].RecommendedPowerPermille, $"stage {i + 1}");
            }
        }

        [Test]
        public void TrainingStage_EnemyStatsComeFromItsRecommendedPower_WhateverTheDifficulty()
        {
            foreach (StageDefinition stage in StageCatalog.Stages)
            {
                CombatStats expected = CombatPowerRules.EnemyStats(stage.RecommendedPowerPermille);
                Assert.AreEqual(expected.InternalPower, stage.EnemyStats.InternalPower, $"stage {stage.StageIndex}");
                Assert.AreEqual(expected.ExternalPower, stage.EnemyStats.ExternalPower, $"stage {stage.StageIndex}");
            }
            Assert.AreEqual(75, StageCatalog.Get(1).EnemyStats.InternalPower);
            Assert.AreEqual(93, StageCatalog.Get(10).EnemyStats.InternalPower);
            // Hard stages use the same 1.8 as the rest: 1.629 / 1.8 = 0.905 -> 95.
            Assert.AreEqual(AiDifficulty.Hard, StageCatalog.Get(11).Difficulty);
            Assert.AreEqual(95, StageCatalog.Get(11).EnemyStats.InternalPower);
            Assert.AreEqual(105, StageCatalog.Get(15).EnemyStats.InternalPower);
        }

        // ---- Story battles ----

        [TestCase("1-1", 1000)]
        [TestCase("1-4", 1158)]
        [TestCase("2-3", 1407)]
        [TestCase("2-4", 1477)]
        [TestCase("2-5", 1551)]
        [TestCase("3-1", 1629)]
        [TestCase("3-4", 1886)]
        [TestCase("3-5", 1980)]
        public void StoryBattle_HasTheApprovedRecommendedPower(string id, int recommendedPermille)
        {
            StoryBook book = StoryDataTests.LoadBook();
            StoryStage stage = book.stages[book.IndexOf(id)];
            Assert.AreEqual(recommendedPermille, stage.recommendedPowerPermille);
            Assert.AreEqual(0, stage.enemyPowerPermille);
            CombatStats expected = CombatPowerRules.EnemyStats(recommendedPermille);
            Assert.AreEqual(expected.InternalPower, stage.EnemyStats.InternalPower);
            Assert.AreEqual(expected.ExternalPower, stage.EnemyStats.ExternalPower);
        }

        [Test]
        public void ForcedLoss_NamesTheEnemyPowerDirectly()
        {
            StoryBook book = StoryDataTests.LoadBook();
            StoryStage stage = book.stages[book.IndexOf("1-5")];
            Assert.AreEqual(0, stage.recommendedPowerPermille);
            Assert.AreEqual(4000, stage.enemyPowerPermille);
            Assert.AreEqual(200, stage.EnemyStats.InternalPower);
            Assert.AreEqual(200, stage.EnemyStats.ExternalPower);
        }

        [Test]
        public void AllNineStoryBattles_HaveAPower_AndStoryStagesHaveNone()
        {
            int withPower = 0;
            foreach (StoryStage s in StoryDataTests.LoadBook().stages)
            {
                if (s.HasBattle)
                {
                    Assert.IsTrue(s.recommendedPowerPermille > 0 ^ s.enemyPowerPermille > 0, $"{s.id}: exactly one of the two powers");
                    Assert.AreEqual(s.Kind == StoryStageKind.Loss, s.enemyPowerPermille > 0, s.id);
                    withPower++;
                }
                else
                {
                    Assert.AreEqual(0, s.recommendedPowerPermille, s.id);
                    Assert.AreEqual(0, s.enemyPowerPermille, s.id);
                }
            }
            Assert.AreEqual(9, withPower);
        }

        [Test]
        public void StoryBattles_RecommendedPowerNeverDecreases()
        {
            int previous = 0;
            foreach (StoryStage s in StoryDataTests.LoadBook().stages)
            {
                if (s.recommendedPowerPermille == 0)
                    continue;
                Assert.GreaterOrEqual(s.recommendedPowerPermille, previous, s.id);
                previous = s.recommendedPowerPermille;
            }
        }

        [Test]
        public void StoryBattles_UseEasyAndNormalTechniquesOnly()
        {
            foreach (StoryStage s in StoryDataTests.LoadBook().stages)
            {
                if (s.HasBattle)
                    Assert.AreNotEqual(AiDifficulty.Hard, StageCatalog.Get(s.aiStage).Difficulty, s.id);
            }
        }

        [Test]
        public void Parse_RejectsAHardAiInTheStory()
            => AssertRejected("\"aiStage\": 1,", "\"aiStage\": 11,");

        [Test]
        public void Parse_RejectsABattleWithoutARecommendedPower()
            => AssertRejected("\"recommendedPowerPermille\": 1000,", "\"recommendedPowerPermille\": 0,");

        [Test]
        public void Parse_RejectsALossStageWithoutAnEnemyPower()
            => AssertRejected("\"enemyPowerPermille\": 4000,", "\"enemyPowerPermille\": 0,");

        [Test]
        public void Parse_RejectsABattleThatAlsoNamesTheEnemyPower()
            => AssertRejected("\"recommendedPowerPermille\": 1000,\n      \"enemyPowerPermille\": 0,", "\"recommendedPowerPermille\": 1000,\n      \"enemyPowerPermille\": 500,");

        [Test]
        public void Parse_RejectsAStoryStageWithAPower()
            => AssertRejected("\"aiStage\": 0,\n      \"recommendedPowerPermille\": 0,", "\"aiStage\": 0,\n      \"recommendedPowerPermille\": 1000,");

        private static void AssertRejected(string find, string replacement)
        {
            string json = StoryDataTests.LoadJson();
            int at = json.IndexOf(find, StringComparison.Ordinal);
            Assert.GreaterOrEqual(at, 0, $"the shipped file no longer contains: {find}");
            string edited = json.Substring(0, at) + replacement + json.Substring(at + find.Length);
            Assert.Throws<StoryDataException>(() => StoryBook.Parse(edited));
        }
    }
}
