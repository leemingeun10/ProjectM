using System.Collections.Generic;
using Game.Client.Story;
using Game.Sim.Progress;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    // The shipped story file (Story/Data/story_part1.json) and StoryBook.Parse's schema checks.
    public class StoryDataTests
    {
        public const string StoryPath = "Assets/Game.Client/Story/Data/story_part1.json";

        public static string LoadJson()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(StoryPath);
            Assert.IsNotNull(asset, StoryPath + " is missing.");
            return asset.text;
        }

        public static StoryBook LoadBook() => StoryBook.Parse(LoadJson());

        // Edits one value of the shipped file and parses the result.
        private static void AssertRejected(string from, string to)
        {
            string json = LoadJson();
            Assert.That(json, Does.Contain(from), "test setup: text to replace not found");
            int at = json.IndexOf(from, System.StringComparison.Ordinal);
            string edited = json.Substring(0, at) + to + json.Substring(at + from.Length);
            Assert.Throws<StoryDataException>(() => StoryBook.Parse(edited));
        }

        [Test]
        public void ShippedFile_ParsesWithFifteenStagesInThreeChapters()
        {
            StoryBook book = LoadBook();
            Assert.AreEqual(StoryBook.CurrentVersion, book.version);
            Assert.AreEqual(15, book.StageCount);
            Assert.AreEqual(3, book.chapters.Length);
            for (int ch = 1; ch <= 3; ch++)
                Assert.AreEqual(5, book.StageIndicesOf(ch).Count, $"chapter {ch}");
        }

        [Test]
        public void ShippedFile_IdsAreUniqueAndInOrder()
        {
            StoryBook book = LoadBook();
            var ids = new HashSet<string>();
            for (int i = 0; i < book.StageCount; i++)
            {
                StoryStage s = book.stages[i];
                Assert.IsTrue(ids.Add(s.id), $"duplicate id {s.id}");
                Assert.AreEqual($"{s.ch}-{s.n}", s.id);
                Assert.AreEqual(i / 5 + 1, s.ch);
                Assert.AreEqual(i % 5 + 1, s.n);
                Assert.AreEqual(i, book.IndexOf(s.id));
            }
        }

        [Test]
        public void ShippedFile_KindsAndClearRulesMatchThePrototype()
        {
            StoryBook book = LoadBook();
            string[] kinds = { "battle", "story", "story", "battle", "loss", "story", "story", "battle", "battle", "boss", "battle", "story", "story", "battle", "boss" };
            for (int i = 0; i < kinds.Length; i++)
            {
                StoryStage s = book.stages[i];
                Assert.AreEqual(kinds[i], s.kind, s.id);
                Assert.AreEqual(kinds[i] == "battle" || kinds[i] == "boss" ? StoryClearRule.Win : StoryClearRule.Always, s.Clear, s.id);
            }
        }

        [Test]
        public void ShippedFile_BattleStagesHaveAnOpponentAndStoryStagesDoNot()
        {
            foreach (StoryStage s in LoadBook().stages)
            {
                if (s.HasBattle)
                {
                    Assert.IsTrue(StageCatalog.IsValidStageIndex(s.aiStage), s.id);
                    Assert.IsNotEmpty(s.enemy, s.id);
                }
                else
                {
                    Assert.AreEqual(0, s.aiStage, s.id);
                    Assert.IsEmpty(s.enemy, s.id);
                    foreach (StoryCut cut in s.cuts)
                        Assert.IsFalse(cut.after, $"{s.id}: a story stage has no after-battle cut");
                }
            }
        }

        [Test]
        public void ShippedFile_EveryCutHasALineAndCloseUpsHaveASpeaker()
        {
            int cuts = 0;
            foreach (StoryStage s in LoadBook().stages)
            {
                Assert.IsNotEmpty(s.cuts, s.id);
                foreach (StoryCut cut in s.cuts)
                {
                    cuts++;
                    Assert.IsNotEmpty(cut.line, s.id);
                    if (cut.Type == StoryCutType.Close)
                        Assert.IsNotEmpty(cut.speaker, s.id);
                }
            }
            // The prototype's ST has 39 cuts.
            Assert.AreEqual(39, cuts);
        }

        [Test]
        public void ShippedFile_OnlyTheTwoFinishedCutsHaveArt()
        {
            var keyed = new List<string>();
            foreach (StoryStage s in LoadBook().stages)
            {
                foreach (StoryCut cut in s.cuts)
                {
                    if (!string.IsNullOrEmpty(cut.image))
                        keyed.Add($"{s.id}:{cut.image}");
                }
            }
            CollectionAssert.AreEqual(new[] { "1-1:Cut_1-1a_B1", "1-2:Cut_1-2a_B1" }, keyed);
        }

        [Test]
        public void ShippedFile_OnlyTheLastStageHasTheChoice()
        {
            StoryBook book = LoadBook();
            for (int i = 0; i < book.StageCount; i++)
                Assert.AreEqual(i == book.StageCount - 1, book.stages[i].choice, book.stages[i].id);
            Assert.IsNotEmpty(book.partEnd.title);
        }

        [Test]
        public void Parse_RejectsAnotherVersion() => AssertRejected("\"version\": 1", "\"version\": 2");

        [Test]
        public void Parse_RejectsEmptyOrBrokenJson()
        {
            Assert.Throws<StoryDataException>(() => StoryBook.Parse(null));
            Assert.Throws<StoryDataException>(() => StoryBook.Parse(""));
            Assert.Throws<StoryDataException>(() => StoryBook.Parse("{ not json"));
        }

        [Test]
        public void Parse_RejectsAnUnknownKind() => AssertRejected("\"kind\": \"loss\"", "\"kind\": \"cutscene\"");

        [Test]
        public void Parse_RejectsALossStageThatMustBeWon() => AssertRejected("\"clear\": \"always\",\n      \"enemy\": \"흑의검객 둘", "\"clear\": \"win\",\n      \"enemy\": \"흑의검객 둘");

        [Test]
        public void Parse_RejectsAnAiStageOutsideTheCatalog() => AssertRejected("\"aiStage\": 1,", "\"aiStage\": 16,");

        [Test]
        public void Parse_RejectsAStoryStageWithAnOpponent() => AssertRejected("\"enemy\": \"\",\n      \"aiStage\": 0", "\"enemy\": \"누군가\",\n      \"aiStage\": 0");

        [Test]
        public void Parse_RejectsADuplicateId() => AssertRejected("\"id\": \"1-2\"", "\"id\": \"1-1\"");

        [Test]
        public void Parse_RejectsACloseUpWithoutSpeaker() => AssertRejected("\"speaker\": \"길바닥 낭인\"", "\"speaker\": \"\"");
    }
}
