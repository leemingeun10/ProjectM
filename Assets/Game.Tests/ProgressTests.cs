using System;
using System.Collections.Generic;
using Game.Sim.Ai;
using Game.Sim.Progress;
using NUnit.Framework;

namespace Game.Tests
{
    public class ProgressTests
    {
        private static ProgressData ClearUpTo(int stageIndex)
        {
            ProgressData progress = default;
            for (int i = 1; i <= stageIndex; i++)
                progress = ProgressRules.RecordStageCleared(progress, i);
            return progress;
        }

        // ---- Unlock rules ----

        [Test]
        public void NewProgress_OnlyStageOneUnlocked()
        {
            ProgressData fresh = default;

            Assert.AreEqual(0, fresh.HighestClearedStage);
            Assert.IsTrue(ProgressRules.IsStageUnlocked(fresh, 1));
            Assert.IsFalse(ProgressRules.IsStageUnlocked(fresh, 2));
        }

        [Test]
        public void ClearingStageOne_UnlocksTwoButNotThree()
        {
            ProgressData progress = ProgressRules.RecordStageCleared(default, 1);

            Assert.AreEqual(1, progress.HighestClearedStage);
            Assert.IsTrue(ProgressRules.IsStageUnlocked(progress, 2));
            Assert.IsFalse(ProgressRules.IsStageUnlocked(progress, 3));
        }

        [Test]
        public void ReclearingEarlierStage_DoesNotLowerRecord()
        {
            ProgressData progress = ClearUpTo(5);

            ProgressData after = ProgressRules.RecordStageCleared(progress, 1);

            Assert.AreEqual(5, after.HighestClearedStage);
            Assert.IsTrue(ProgressRules.IsStageUnlocked(after, 6));
        }

        [Test]
        public void OutOfCatalogStages_AreNeverUnlocked()
        {
            ProgressData all = ClearUpTo(StageCatalog.StageCount);

            Assert.IsFalse(ProgressRules.IsStageUnlocked(all, 0));
            Assert.IsFalse(ProgressRules.IsStageUnlocked(all, -1));
            Assert.IsFalse(ProgressRules.IsStageUnlocked(all, StageCatalog.StageCount + 1));
        }

        [Test]
        public void RecordStageCleared_RejectsInvalidOrLockedStage()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ProgressRules.RecordStageCleared(default, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ProgressRules.RecordStageCleared(default, StageCatalog.StageCount + 1));
            Assert.Throws<InvalidOperationException>(() => ProgressRules.RecordStageCleared(default, 3));
        }

        // ---- PvP unlock ----

        [Test]
        public void Pvp_UnlocksExactlyAtThreshold()
        {
            for (int cleared = 0; cleared < StageCatalog.PvpUnlockStageIndex; cleared++)
                Assert.IsFalse(ProgressRules.IsPvpUnlocked(ClearUpTo(cleared)), $"cleared {cleared}");

            for (int cleared = StageCatalog.PvpUnlockStageIndex; cleared <= StageCatalog.StageCount; cleared++)
                Assert.IsTrue(ProgressRules.IsPvpUnlocked(ClearUpTo(cleared)), $"cleared {cleared}");
        }

        // ---- Serialization ----

        [Test]
        public void RoundTrip_RestoresHighestClearedStage()
        {
            for (int cleared = 0; cleared <= StageCatalog.StageCount; cleared++)
            {
                string json = ProgressJsonSerializer.Serialize(new ProgressData(cleared));
                (int version, ProgressData restored) = ProgressJsonSerializer.Deserialize(json);

                Assert.AreEqual(ProgressJsonSerializer.CurrentVersion, version);
                Assert.AreEqual(cleared, restored.HighestClearedStage);
            }
        }

        [Test]
        public void Serialize_WritesVersionField()
        {
            string json = ProgressJsonSerializer.Serialize(new ProgressData(7));

            StringAssert.Contains("\"version\": 1", json);
            StringAssert.Contains("\"highestClearedStage\": 7", json);
        }

        [Test]
        public void Deserialize_RejectsBadInput()
        {
            Assert.Throws<ProgressJsonException>(() => ProgressJsonSerializer.Deserialize("{\"version\": 2, \"highestClearedStage\": 1}"));
            Assert.Throws<ProgressJsonException>(() => ProgressJsonSerializer.Deserialize("{\"highestClearedStage\": 1}"));
            Assert.Throws<ProgressJsonException>(() => ProgressJsonSerializer.Deserialize("{\"version\": 1}"));
            Assert.Throws<ProgressJsonException>(() => ProgressJsonSerializer.Deserialize("{\"version\": 1, \"highestClearedStage\": -1}"));
            Assert.Throws<ProgressJsonException>(() => ProgressJsonSerializer.Deserialize("{\"version\": 1, \"highestClearedStage\": 16}"));
            Assert.Throws<ProgressJsonException>(() => ProgressJsonSerializer.Deserialize("{\"version\": 1, \"highestClearedStage\": \"3\"}"));
            Assert.Throws<ProgressJsonException>(() => ProgressJsonSerializer.Deserialize("{\"version\": 1, "));
            Assert.Throws<ProgressJsonException>(() => ProgressJsonSerializer.Deserialize("[]"));
        }

        // ---- Catalog ----

        [Test]
        public void Catalog_SeedsAreAllDistinct()
        {
            Assert.AreEqual(15, StageCatalog.StageCount);
            Assert.AreEqual(StageCatalog.StageCount, StageCatalog.Stages.Count);

            var seeds = new HashSet<ulong>();
            foreach (StageDefinition stage in StageCatalog.Stages)
                Assert.IsTrue(seeds.Add(stage.Seed), $"stage {stage.StageIndex} reuses seed {stage.Seed:X}");
        }

        [Test]
        public void Catalog_DifficultyDistributionAndIndices()
        {
            for (int i = 1; i <= StageCatalog.StageCount; i++)
            {
                StageDefinition stage = StageCatalog.Get(i);
                Assert.AreEqual(i, stage.StageIndex);

                AiDifficulty expected = i <= 5 ? AiDifficulty.Easy : i <= 10 ? AiDifficulty.Normal : AiDifficulty.Hard;
                Assert.AreEqual(expected, stage.Difficulty, $"stage {i}");
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => StageCatalog.Get(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => StageCatalog.Get(StageCatalog.StageCount + 1));
        }

        [Test]
        public void Catalog_SameStageAlwaysGeneratesSameOpponent()
        {
            StageDefinition stage = StageCatalog.Get(12);
            var a = AiTechniqueGenerator.Generate(stage.Difficulty, stage.Seed);
            var b = AiTechniqueGenerator.Generate(stage.Difficulty, stage.Seed);

            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].IsCounter, b[i].IsCounter);
                Assert.AreEqual(a[i].RawPoints[0].X.Raw, b[i].RawPoints[0].X.Raw);
                Assert.AreEqual(a[i].RawPoints[0].Y.Raw, b[i].RawPoints[0].Y.Raw);
            }
        }
    }
}
