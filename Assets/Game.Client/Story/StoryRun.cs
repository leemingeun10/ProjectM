using System;
using System.Collections.Generic;
using Game.Sim.Battle;

namespace Game.Client.Story
{
    public enum StoryStepKind
    {
        Cut,
        Battle,
        // Shown after losing a stage that must be won: fight again or leave.
        Lost,
        Choice,
        // After the choice: the end-of-part panel.
        End,
    }

    public readonly struct StoryStep
    {
        public readonly StoryStepKind Kind;
        public readonly StoryCut Cut;

        public StoryStep(StoryStepKind kind, StoryCut cut = null)
        {
            Kind = kind;
            Cut = cut;
        }
    }

    // One play-through of a stage, the prototype's play() / step() rules without any UI:
    // the cuts before the battle, the battle (unless it is a story stage), the cuts after it,
    // then the choice if the stage has one. The UI shows Current and calls the method matching it.
    public class StoryRun
    {
        private readonly List<StoryStep> steps = new List<StoryStep>();
        private int position;

        public StoryStage Stage { get; }
        // 0-based position in StoryBook.stages.
        public int StageIndex { get; }
        public StoryFlag ChosenFlag { get; private set; }

        public StoryRun(StoryStage stage, int stageIndex)
        {
            Stage = stage ?? throw new ArgumentNullException(nameof(stage));
            StageIndex = stageIndex;

            foreach (StoryCut cut in stage.cuts)
            {
                if (!cut.after)
                    steps.Add(new StoryStep(StoryStepKind.Cut, cut));
            }
            if (stage.HasBattle)
                steps.Add(new StoryStep(StoryStepKind.Battle));
            foreach (StoryCut cut in stage.cuts)
            {
                if (cut.after)
                    steps.Add(new StoryStep(StoryStepKind.Cut, cut));
            }
            if (stage.choice)
                steps.Add(new StoryStep(StoryStepKind.Choice));
        }

        public bool IsFinished => position >= steps.Count;

        public StoryStep Current => IsFinished ? throw new InvalidOperationException("The stage is finished.") : steps[position];

        public int CutTotal => Stage.cuts.Length;

        // 1-based number of the cut being shown (or of the last cut shown before a non-cut step).
        public int CutNumber
        {
            get
            {
                int count = 0;
                for (int i = 0; i <= position && i < steps.Count; i++)
                {
                    if (steps[i].Kind == StoryStepKind.Cut)
                        count++;
                }
                return count;
            }
        }

        // The remaining steps, current first (for tests and debugging).
        public IReadOnlyList<StoryStepKind> RemainingKinds
        {
            get
            {
                var kinds = new List<StoryStepKind>();
                for (int i = position; i < steps.Count; i++)
                    kinds.Add(steps[i].Kind);
                return kinds;
            }
        }

        // Tap through a cut, or leave the end panel.
        public void Next()
        {
            Require(StoryStepKind.Cut, StoryStepKind.End);
            position++;
        }

        // A win moves on; so does any outcome on a stage that clears regardless (1-5). Otherwise
        // (loss or draw on a stage that must be won) the Lost step comes next.
        public void ReportBattle(BattleOutcome outcome)
        {
            Require(StoryStepKind.Battle);
            if (outcome != BattleOutcome.WinA && Stage.Clear == StoryClearRule.Win)
                steps.Insert(position + 1, new StoryStep(StoryStepKind.Lost));
            position++;
        }

        // From the Lost step back to the battle.
        public void Retry()
        {
            Require(StoryStepKind.Lost);
            steps.RemoveAt(position);
            position--;
        }

        public void Choose(StoryFlag flag)
        {
            Require(StoryStepKind.Choice);
            if (flag == StoryFlag.None)
                throw new ArgumentException("A choice must pick a side.", nameof(flag));
            ChosenFlag = flag;
            steps.Insert(position + 1, new StoryStep(StoryStepKind.End));
            position++;
        }

        private void Require(StoryStepKind kind, StoryStepKind? alternative = null)
        {
            if (IsFinished)
                throw new InvalidOperationException("The stage is finished.");
            StoryStepKind current = steps[position].Kind;
            if (current != kind && current != alternative)
                throw new InvalidOperationException($"Expected a {kind} step, but the current step is {current}.");
        }
    }
}
