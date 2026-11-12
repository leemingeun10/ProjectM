using Game.Client.Scenery;
using Game.Client.Stage;
using NUnit.Framework;

namespace Game.Tests
{
    public class ArenaThemeTests
    {
        [TestCase(1, ArenaTheme.Street)]
        [TestCase(2, ArenaTheme.TrainingHall)]
        [TestCase(3, ArenaTheme.MountHua)]
        public void ForChapter_GivesEachChapterItsOwnSet(int chapter, ArenaTheme expected)
        {
            Assert.AreEqual(expected, ArenaThemes.ForChapter(chapter));
        }

        [Test]
        public void ForChapter_KeepsTheLastSetPastTheThirdChapter()
        {
            Assert.AreEqual(ArenaTheme.MountHua, ArenaThemes.ForChapter(4));
        }

        [TestCase(1, ArenaTheme.Street)]
        [TestCase(5, ArenaTheme.Street)]
        [TestCase(6, ArenaTheme.TrainingHall)]
        [TestCase(11, ArenaTheme.MountHua)]
        [TestCase(15, ArenaTheme.MountHua)]
        public void Stages_FightInTheirChaptersSet(int stageIndex, ArenaTheme expected)
        {
            Assert.AreEqual(expected, ArenaThemes.ForChapter(StageChapters.ChapterOf(stageIndex)));
        }
    }
}
