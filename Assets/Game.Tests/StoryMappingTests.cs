using Game.Client.Battle;
using Game.Client.Scenery;
using Game.Client.Story;
using Game.Sim.Battle;
using Game.Sim.Progress;
using Game.Sim.Ai;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests
{
    // How story battles map onto the existing battle: opponent technique (StageCatalog), model and
    // set, per the approved (provisional) table; plus the 1-1 starter technique and the story art.
    public class StoryMappingTests
    {
        private static StoryBook book;

        [OneTimeSetUp]
        public void LoadBook() => book = StoryDataTests.LoadBook();

        [TestCase("1-1", 1, EnemyLook.Ronin, ArenaTheme.Street)]
        [TestCase("1-4", 2, EnemyLook.Ronin, ArenaTheme.Street)]
        [TestCase("1-5", 15, EnemyLook.BlackSwordsman, ArenaTheme.Street)]
        [TestCase("2-3", 6, EnemyLook.BlackSwordsman, ArenaTheme.TrainingHall)]
        [TestCase("2-4", 8, EnemyLook.BlackSwordsman, ArenaTheme.TrainingHall)]
        [TestCase("2-5", 10, EnemyLook.BlackSwordsman, ArenaTheme.TrainingHall)]
        [TestCase("3-1", 11, EnemyLook.PlumSwordsman, ArenaTheme.MountHua)]
        [TestCase("3-4", 13, EnemyLook.PlumSwordsman, ArenaTheme.MountHua)]
        [TestCase("3-5", 15, EnemyLook.PlumSwordsman, ArenaTheme.MountHua)]
        public void BattleStage_UsesTheApprovedOpponentModelAndSet(string id, int aiStage, EnemyLook look, ArenaTheme theme)
        {
            StoryStage stage = book.stages[book.IndexOf(id)];
            Assert.IsTrue(stage.HasBattle);
            Assert.AreEqual(aiStage, stage.aiStage);
            Assert.AreEqual(look, stage.Look);
            Assert.AreEqual(theme, stage.Theme);
        }

        [Test]
        public void ExactlyNineStagesHaveABattle()
        {
            int battles = 0;
            foreach (StoryStage s in book.stages)
            {
                if (s.HasBattle)
                    battles++;
            }
            Assert.AreEqual(9, battles);
        }

        [Test]
        public void OnlyTheForcedLoss_RelabelsTheOutcome()
        {
            foreach (StoryStage s in book.stages)
                Assert.AreEqual(s.id == "1-5" ? "기절" : string.Empty, s.outcomeLabel ?? string.Empty, s.id);
        }

        [Test]
        public void OnlyTheFirstStage_OffersTheStarterTechnique()
        {
            foreach (StoryStage s in book.stages)
                Assert.AreEqual(s.id == "1-1", s.starter, s.id);
        }

        [Test]
        public void StarterTechnique_IsAFullSequenceThatBeatsTheFirstOpponent()
        {
            StoryStage first = book.stages[0];
            var starter = StarterTechnique.Create();
            Assert.AreEqual(BattleSimulator.TurnsPerBattle, starter.Count);
            StageDefinition opponent = StageCatalog.Get(first.aiStage);
            BattleResult result = BattleSimulator.Simulate(starter, AiTechniqueGenerator.Generate(opponent.Difficulty, opponent.Seed));
            Assert.AreEqual(BattleOutcome.WinA, result.Outcome);
        }

        [Test]
        public void StarterTechnique_IsTheSameEveryTime()
        {
            var a = StarterTechnique.Create();
            var b = StarterTechnique.Create();
            for (int i = 0; i < a.Count; i++)
                CollectionAssert.AreEqual(a[i].RawPoints, b[i].RawPoints);
        }

        [TestCase("Ronin", true)]
        [TestCase("Default", false)]
        [TestCase("ronin", false)]
        [TestCase("1", false)]
        [TestCase("", false)]
        public void LookNames_AreParsedStrictly(string value, bool ok) => Assert.AreEqual(ok, StoryBook.TryParseLook(value, out _));

        [TestCase("MountHua", true)]
        [TestCase("3", false)]
        [TestCase("Mountain", false)]
        public void ThemeNames_AreParsedStrictly(string value, bool ok) => Assert.AreEqual(ok, StoryBook.TryParseTheme(value, out _));

        [Test]
        public void ArtCatalog_HasEveryImageTheStoryNames()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StoryArtCatalog>("Assets/Game.Client/Story/Data/StoryArtCatalog.asset");
            Assert.IsNotNull(catalog);
            foreach (StoryStage s in book.stages)
            {
                foreach (StoryCut cut in s.cuts)
                {
                    if (!string.IsNullOrEmpty(cut.image))
                        Assert.IsNotNull(catalog.Find(cut.image), $"{s.id}: {cut.image}");
                }
            }
            Assert.IsNull(catalog.Find(""));
            Assert.IsNull(catalog.Find("Cut_9-9z"));
        }
    }
}
