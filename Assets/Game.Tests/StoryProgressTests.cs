using System;
using Game.Client.Story;
using NUnit.Framework;

namespace Game.Tests
{
    // Story save rules (the prototype's stateOf / finish) and the storymap.json format.
    public class StoryProgressTests
    {
        private const int Count = 15;

        [Test]
        public void FreshSave_FirstStageIsCurrentAndTheRestLocked()
        {
            var progress = default(StoryProgress);
            Assert.AreEqual(StoryStageState.Current, StoryProgressRules.StateOf(progress, 0));
            Assert.AreEqual(StoryStageState.Locked, StoryProgressRules.StateOf(progress, 1));
            Assert.IsTrue(StoryProgressRules.CanPlay(progress, 0, Count));
            Assert.IsFalse(StoryProgressRules.CanPlay(progress, 1, Count));
        }

        [Test]
        public void StateOf_SplitsClearedCurrentAndLocked()
        {
            var progress = new StoryProgress(3, StoryFlag.None);
            Assert.AreEqual(StoryStageState.Cleared, StoryProgressRules.StateOf(progress, 2));
            Assert.AreEqual(StoryStageState.Current, StoryProgressRules.StateOf(progress, 3));
            Assert.AreEqual(StoryStageState.Locked, StoryProgressRules.StateOf(progress, 4));
        }

        [Test]
        public void FinishingTheCurrentStage_MovesOnByOne()
        {
            var progress = new StoryProgress(3, StoryFlag.None);
            Assert.IsTrue(StoryProgressRules.IsFirstClear(progress, 3));
            Assert.AreEqual(4, StoryProgressRules.RecordFinished(progress, 3, Count).Cleared);
        }

        [Test]
        public void ReplayingAClearedStage_ChangesNothing()
        {
            var progress = new StoryProgress(3, StoryFlag.Jeong);
            Assert.IsFalse(StoryProgressRules.IsFirstClear(progress, 1));
            StoryProgress after = StoryProgressRules.RecordFinished(progress, 1, Count);
            Assert.AreEqual(3, after.Cleared);
            Assert.AreEqual(StoryFlag.Jeong, after.Flag);
        }

        [Test]
        public void FinishingALockedOrMissingStage_Throws()
        {
            var progress = new StoryProgress(3, StoryFlag.None);
            Assert.Throws<InvalidOperationException>(() => StoryProgressRules.RecordFinished(progress, 4, Count));
            Assert.Throws<InvalidOperationException>(() => StoryProgressRules.RecordFinished(progress, -1, Count));
            Assert.Throws<InvalidOperationException>(() => StoryProgressRules.RecordFinished(new StoryProgress(Count, StoryFlag.None), Count, Count));
        }

        [Test]
        public void WithFlag_KeepsTheClearCount()
        {
            StoryProgress after = StoryProgressRules.WithFlag(new StoryProgress(14, StoryFlag.None), StoryFlag.Sa);
            Assert.AreEqual(14, after.Cleared);
            Assert.AreEqual(StoryFlag.Sa, after.Flag);
        }

        [Test]
        public void CurrentChapter_FollowsTheNextStageAndStaysOnTheLastWhenAllAreCleared()
        {
            StoryBook book = StoryDataTests.LoadBook();
            Assert.AreEqual(1, StoryProgressRules.CurrentChapter(book, default));
            Assert.AreEqual(2, StoryProgressRules.CurrentChapter(book, new StoryProgress(5, StoryFlag.None)));
            Assert.AreEqual(3, StoryProgressRules.CurrentChapter(book, new StoryProgress(15, StoryFlag.None)));
        }

        [TestCase(0, StoryFlag.None)]
        [TestCase(7, StoryFlag.None)]
        [TestCase(15, StoryFlag.Jeong)]
        [TestCase(15, StoryFlag.Sa)]
        public void Json_RoundTrips(int cleared, StoryFlag flag)
        {
            string json = StoryProgressJson.Serialize(new StoryProgress(cleared, flag));
            StringAssert.Contains("\"version\": 1", json);
            StoryProgress back = StoryProgressJson.Deserialize(json, Count);
            Assert.AreEqual(cleared, back.Cleared);
            Assert.AreEqual(flag, back.Flag);
        }

        [Test]
        public void Json_ReadsThePrototypeFlagNames()
        {
            StoryProgress p = StoryProgressJson.Deserialize("{\"version\":1,\"cleared\":15,\"flag\":\"jeong\"}", Count);
            Assert.AreEqual(StoryFlag.Jeong, p.Flag);
        }

        [TestCase("{\"version\":2,\"cleared\":0,\"flag\":\"\"}")]
        [TestCase("{\"cleared\":0,\"flag\":\"\"}")]
        [TestCase("{\"version\":1,\"cleared\":16,\"flag\":\"\"}")]
        [TestCase("{\"version\":1,\"cleared\":-1,\"flag\":\"\"}")]
        [TestCase("{\"version\":1,\"cleared\":3,\"flag\":\"neutral\"}")]
        [TestCase("not json")]
        [TestCase("")]
        public void Json_RejectsBadFiles(string json)
        {
            Assert.Throws<StoryProgressException>(() => StoryProgressJson.Deserialize(json, Count));
        }
    }
}
