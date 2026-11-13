using Game.Client.Battle;
using Game.Client.Stage;
using NUnit.Framework;

namespace Game.Tests
{
    public class EnemyLookTests
    {
        [TestCase(1, EnemyLook.Ronin)]
        [TestCase(2, EnemyLook.BlackSwordsman)]
        public void ForChapter_GivesEachChapterItsEnemy(int chapter, EnemyLook expected)
        {
            Assert.AreEqual(expected, EnemyLooks.ForChapter(chapter));
        }

        [Test]
        public void ForChapter_KeepsTheLastEnemyPastTheTable()
        {
            Assert.AreEqual(EnemyLooks.ForChapter(EnemyLooks.ChaptersDefined), EnemyLooks.ForChapter(EnemyLooks.ChaptersDefined + 1));
        }

        [Test]
        public void ForChapter_TreatsChaptersBelowOneAsTheFirst()
        {
            Assert.AreEqual(EnemyLooks.ForChapter(1), EnemyLooks.ForChapter(0));
        }

        [TestCase(1, EnemyLook.Ronin)]
        [TestCase(5, EnemyLook.Ronin)]
        [TestCase(6, EnemyLook.BlackSwordsman)]
        [TestCase(10, EnemyLook.BlackSwordsman)]
        public void Stages_FaceTheirChaptersEnemy(int stageIndex, EnemyLook expected)
        {
            Assert.AreEqual(expected, EnemyLooks.ForChapter(StageChapters.ChapterOf(stageIndex)));
        }

        [Test]
        public void Workshop_FacesTheRonin()
        {
            Assert.AreEqual(EnemyLook.Ronin, EnemyLooks.Workshop);
        }
    }
}
