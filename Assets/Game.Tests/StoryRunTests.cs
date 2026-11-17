using System;
using Game.Client.Story;
using Game.Sim.Battle;
using NUnit.Framework;

namespace Game.Tests
{
    // StoryRun: the prototype's play() / step() order and its defeat / choice rules.
    public class StoryRunTests
    {
        private static StoryBook book;

        [OneTimeSetUp]
        public void LoadBook() => book = StoryDataTests.LoadBook();

        private static StoryRun Start(string id)
        {
            int index = book.IndexOf(id);
            return new StoryRun(book.stages[index], index);
        }

        private static void TapThroughCuts(StoryRun run)
        {
            while (!run.IsFinished && run.Current.Kind == StoryStepKind.Cut)
                run.Next();
        }

        [Test]
        public void BattleStage_CutsBeforeThenBattleThenCutsAfter()
        {
            // 1-1: three cuts before the battle, two after.
            StoryRun run = Start("1-1");
            CollectionAssert.AreEqual(new[]
            {
                StoryStepKind.Cut, StoryStepKind.Cut, StoryStepKind.Cut, StoryStepKind.Battle, StoryStepKind.Cut, StoryStepKind.Cut,
            }, run.RemainingKinds);
            Assert.IsFalse(run.Current.Cut.after);
        }

        [Test]
        public void StoryStage_HasNoBattle()
        {
            StoryRun run = Start("1-2");
            CollectionAssert.AreEqual(new[] { StoryStepKind.Cut, StoryStepKind.Cut, StoryStepKind.Cut }, run.RemainingKinds);
            TapThroughCuts(run);
            Assert.IsTrue(run.IsFinished);
        }

        [Test]
        public void CutNumber_CountsCutsAcrossTheBattle()
        {
            StoryRun run = Start("1-1");
            Assert.AreEqual(5, run.CutTotal);
            Assert.AreEqual(1, run.CutNumber);
            TapThroughCuts(run);
            Assert.AreEqual(StoryStepKind.Battle, run.Current.Kind);
            run.ReportBattle(BattleOutcome.WinA);
            Assert.AreEqual(4, run.CutNumber);
        }

        [Test]
        public void Win_MovesOnToTheCutsAfter()
        {
            StoryRun run = Start("1-1");
            TapThroughCuts(run);
            run.ReportBattle(BattleOutcome.WinA);
            Assert.AreEqual(StoryStepKind.Cut, run.Current.Kind);
            Assert.IsTrue(run.Current.Cut.after);
            TapThroughCuts(run);
            Assert.IsTrue(run.IsFinished);
        }

        [TestCase(BattleOutcome.WinB)]
        [TestCase(BattleOutcome.Draw)]
        public void LosingAStageThatMustBeWon_ShowsDefeatAndRetryReturnsToTheBattle(BattleOutcome outcome)
        {
            StoryRun run = Start("1-1");
            TapThroughCuts(run);
            run.ReportBattle(outcome);
            Assert.AreEqual(StoryStepKind.Lost, run.Current.Kind);

            run.Retry();
            Assert.AreEqual(StoryStepKind.Battle, run.Current.Kind);
            run.ReportBattle(BattleOutcome.WinA);
            Assert.IsTrue(run.Current.Cut.after);
            // The defeat step is gone: two cuts after the battle remain.
            CollectionAssert.AreEqual(new[] { StoryStepKind.Cut, StoryStepKind.Cut }, run.RemainingKinds);
        }

        [TestCase(BattleOutcome.WinA)]
        [TestCase(BattleOutcome.WinB)]
        [TestCase(BattleOutcome.Draw)]
        public void LossStage_MovesOnWhateverTheOutcome(BattleOutcome outcome)
        {
            StoryRun run = Start("1-5");
            TapThroughCuts(run);
            run.ReportBattle(outcome);
            Assert.AreEqual(StoryStepKind.Cut, run.Current.Kind);
            Assert.IsTrue(run.Current.Cut.after);
        }

        [Test]
        public void FinalStage_ChoiceRecordsTheFlagThenShowsTheEnd()
        {
            StoryRun run = Start("3-5");
            TapThroughCuts(run);
            run.ReportBattle(BattleOutcome.WinA);
            TapThroughCuts(run);
            Assert.AreEqual(StoryStepKind.Choice, run.Current.Kind);

            run.Choose(StoryFlag.Sa);
            Assert.AreEqual(StoryFlag.Sa, run.ChosenFlag);
            Assert.AreEqual(StoryStepKind.End, run.Current.Kind);
            run.Next();
            Assert.IsTrue(run.IsFinished);
        }

        [Test]
        public void Choice_MustPickASide()
        {
            StoryRun run = Start("3-5");
            TapThroughCuts(run);
            run.ReportBattle(BattleOutcome.WinA);
            TapThroughCuts(run);
            Assert.Throws<ArgumentException>(() => run.Choose(StoryFlag.None));
        }

        [Test]
        public void WrongStepCalls_Throw()
        {
            StoryRun run = Start("1-1");
            Assert.Throws<InvalidOperationException>(() => run.ReportBattle(BattleOutcome.WinA));
            Assert.Throws<InvalidOperationException>(() => run.Retry());
            Assert.Throws<InvalidOperationException>(() => run.Choose(StoryFlag.Jeong));
            TapThroughCuts(run);
            Assert.Throws<InvalidOperationException>(() => run.Next());
        }
    }
}
