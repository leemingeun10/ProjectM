using Game.Client.Stage;
using Game.Sim.Progress;
using NUnit.Framework;

namespace Game.Tests
{
    public class StageChaptersTests
    {
        [Test]
        public void Catalog_SplitsIntoThreeChaptersOfFive()
        {
            Assert.AreEqual(3, StageChapters.ChapterCount);
            for (int chapter = 1; chapter <= StageChapters.ChapterCount; chapter++)
                Assert.AreEqual(StageChapters.StagesPerChapter, StageChapters.LastStage(chapter) - StageChapters.FirstStage(chapter) + 1);
        }

        [TestCase(1, 1)]
        [TestCase(5, 1)]
        [TestCase(6, 2)]
        [TestCase(10, 2)]
        [TestCase(11, 3)]
        [TestCase(15, 3)]
        public void ChapterOf_GroupsConsecutiveStages(int stageIndex, int expectedChapter)
        {
            Assert.AreEqual(expectedChapter, StageChapters.ChapterOf(stageIndex));
        }

        [Test]
        public void Boss_IsLastStageOfEachChapter()
        {
            for (int stage = 1; stage <= StageCatalog.StageCount; stage++)
                Assert.AreEqual(stage == 5 || stage == 10 || stage == 15, StageChapters.IsBoss(stage), $"stage {stage}");
        }

        [Test]
        public void PvpUnlockStage_IsChapterTwoBoss()
        {
            Assert.AreEqual(2, StageChapters.ChapterOf(StageCatalog.PvpUnlockStageIndex));
            Assert.IsTrue(StageChapters.IsBoss(StageCatalog.PvpUnlockStageIndex));
        }

        [TestCase(0, true, false, false)]
        [TestCase(4, true, false, false)]
        [TestCase(5, true, true, false)]
        [TestCase(9, true, true, false)]
        [TestCase(10, true, true, true)]
        [TestCase(15, true, true, true)]
        public void Chapter_OpensWhenPreviousBossIsCleared(int highestCleared, bool ch1, bool ch2, bool ch3)
        {
            var progress = new ProgressData(highestCleared);

            Assert.AreEqual(ch1, StageChapters.IsUnlocked(progress, 1));
            Assert.AreEqual(ch2, StageChapters.IsUnlocked(progress, 2));
            Assert.AreEqual(ch3, StageChapters.IsUnlocked(progress, 3));
        }

        [TestCase(0, 0, 0)]
        [TestCase(3, 3, 0)]
        [TestCase(7, 5, 2)]
        [TestCase(15, 5, 5)]
        public void ClearedCount_IsClampedToChapter(int highestCleared, int ch1, int ch2)
        {
            var progress = new ProgressData(highestCleared);

            Assert.AreEqual(ch1, StageChapters.ClearedCount(progress, 1));
            Assert.AreEqual(ch2, StageChapters.ClearedCount(progress, 2));
        }
    }
}
