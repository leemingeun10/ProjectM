using System.Collections.Generic;
using Game.Client.Battle;
using Game.Client.Stage;
using Game.Client.Story;
using Game.Sim.Ai;
using Game.Sim.Battle;
using Game.Sim.Cultivation;
using Game.Sim.Progress;
using Game.Sim.Technique;
using NUnit.Framework;

namespace Game.Tests
{
    // Which stats each kind of battle is fought with (BattleMatchups) and how power is written.
    public class BattleMatchupsTests
    {
        private static readonly List<TechniqueStep> Player = AiTechniqueGenerator.Generate(AiDifficulty.Normal, 0x51A7UL);

        // CultivationStats(external, internal, date)
        private static readonly CultivationStats Trained = new CultivationStats(40, 70, 20261120);

        private static List<TechniqueStep> AiOf(StageDefinition stage) => AiTechniqueGenerator.Generate(stage.Difficulty, stage.Seed);

        private static void AssertSame(BattleResult expected, BattleResult actual)
        {
            CollectionAssert.AreEqual(expected.Events, actual.Events);
            Assert.AreEqual(expected.FinalHpA, actual.FinalHpA);
            Assert.AreEqual(expected.FinalHpB, actual.FinalHpB);
            Assert.AreEqual(expected.MaxHpA, actual.MaxHpA);
            Assert.AreEqual(expected.MaxHpB, actual.MaxHpB);
            Assert.AreEqual(expected.Outcome, actual.Outcome);
        }

        [Test]
        public void Training_PlayerUsesTrainedStats_EnemyUsesTheStages()
        {
            foreach (StageDefinition stage in StageCatalog.Stages)
            {
                List<TechniqueStep> ai = AiOf(stage);
                BattleResult result = BattleMatchups.Training(Player, ai, stage, Trained);
                Assert.AreEqual(170, result.MaxHpA, $"stage {stage.StageIndex}: 100 + 내공 70");
                Assert.AreEqual(stage.EnemyStats.InternalPower, result.MaxHpB, $"stage {stage.StageIndex}");
                AssertSame(BattleSimulator.Simulate(Player, ai, new CombatStats(170, 140), stage.EnemyStats), result);
            }
        }

        [Test]
        public void Training_AnUntrainedPlayerIsOneHundredEach()
        {
            StageDefinition stage = StageCatalog.Get(1);
            BattleResult result = BattleMatchups.Training(Player, AiOf(stage), stage, default);
            Assert.AreEqual(100, result.MaxHpA);
            Assert.AreEqual(75, result.MaxHpB);
        }

        [Test]
        public void Story_EnemyUsesTheStoryStagesOwnStats_NotTheAiStages()
        {
            StoryBook book = StoryDataTests.LoadBook();
            foreach (StoryStage s in book.stages)
            {
                if (!s.HasBattle)
                    continue;
                List<TechniqueStep> ai = AiOf(StageCatalog.Get(s.aiStage));
                BattleResult result = BattleMatchups.Story(Player, ai, s.EnemyStats, Trained);
                Assert.AreEqual(170, result.MaxHpA, s.id);
                Assert.AreEqual(s.EnemyStats.InternalPower, result.MaxHpB, s.id);
                AssertSame(BattleSimulator.Simulate(Player, ai, new CombatStats(170, 140), s.EnemyStats), result);
            }
            // 3-5 borrows stage 7's technique but is tuned for 1.98, not stage 7's 1.34.
            StoryStage boss = book.stages[book.IndexOf("3-5")];
            Assert.AreEqual(105, boss.EnemyStats.InternalPower);
            Assert.AreEqual(86, StageCatalog.Get(boss.aiStage).EnemyStats.InternalPower);
        }

        [Test]
        public void Story_TheStarterTechniqueFightsWithThePlayersStats()
        {
            StoryBook book = StoryDataTests.LoadBook();
            StoryStage first = book.stages[0];
            Assert.IsTrue(first.starter);
            List<TechniqueStep> starter = StarterTechnique.Create();
            List<TechniqueStep> ai = AiOf(StageCatalog.Get(first.aiStage));

            BattleResult fresh = BattleMatchups.Story(starter, ai, first.EnemyStats, default);
            Assert.AreEqual(100, fresh.MaxHpA);
            Assert.AreEqual(75, fresh.MaxHpB);
            Assert.AreEqual(BattleOutcome.WinA, fresh.Outcome, "a new player wins 1-1 with the starter technique");

            BattleResult trained = BattleMatchups.Story(starter, ai, first.EnemyStats, Trained);
            Assert.AreEqual(170, trained.MaxHpA);
        }

        [Test]
        public void Sparring_IsOneHundredEach_WhateverThePlayerTrained()
        {
            List<TechniqueStep> opponent = AiTechniqueGenerator.Generate(AiDifficulty.Hard, 0xBEEFUL);
            BattleResult result = BattleMatchups.Sparring(Player, opponent);
            Assert.AreEqual(100, result.MaxHpA);
            Assert.AreEqual(100, result.MaxHpB);
            AssertSame(BattleSimulator.Simulate(Player, opponent), result);
        }

        [Test]
        public void PlayerStats_AreOneHundredPlusTraining()
        {
            CombatStats stats = BattleMatchups.PlayerStats(Trained);
            Assert.AreEqual(170, stats.InternalPower);
            Assert.AreEqual(140, stats.ExternalPower);
        }

        [TestCase(1000, "1.0")]
        [TestCase(1100, "1.1")]
        [TestCase(1210, "1.21")]
        [TestCase(4410, "4.41")]
        [TestCase(563, "0.56")]
        [TestCase(565, "0.57")]
        [TestCase(2380, "2.38")]
        [TestCase(1996, "2.0")]
        [TestCase(4200, "4.2")]
        [TestCase(10050, "10.05")]
        [TestCase(0, "0.0")]
        public void CombatPowerText_ShowsUpToTwoDecimals(int permille, string expected)
            => Assert.AreEqual(expected, CombatPowerText.Format(permille));

        [Test]
        public void CombatPowerText_OfStats()
        {
            Assert.AreEqual("1.0", CombatPowerText.Format(CombatStats.Default));
            Assert.AreEqual("2.38", CombatPowerText.Format(new CombatStats(170, 140)));
        }
    }
}
