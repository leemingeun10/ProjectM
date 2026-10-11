using System.Collections.Generic;
using Game.Sim.Stroke;

namespace Game.Sim.Technique
{
    public class TechniqueBuilder
    {
        public const int MaxTurns = 10;
        public const int MaxCounters = 2;

        private readonly List<TechniqueStep> steps = new List<TechniqueStep>();
        private int counterCount;

        public IReadOnlyList<TechniqueStep> Steps => steps;
        public int TurnCount => steps.Count;
        public int RemainingCounters => MaxCounters - counterCount;
        public bool IsFull => steps.Count >= MaxTurns;

        public TechniqueAddResult TryAddStep(bool isCounter, StrokeResult stroke, IReadOnlyList<StrokePoint> rawPoints)
        {
            if (steps.Count >= MaxTurns)
                return TechniqueAddResult.SequenceFull;

            if (isCounter && counterCount >= MaxCounters)
                return TechniqueAddResult.CounterLimitReached;

            steps.Add(new TechniqueStep(steps.Count, isCounter, stroke, rawPoints));
            if (isCounter)
                counterCount++;

            return TechniqueAddResult.Success;
        }

        // Removes one turn and renumbers the later ones so TurnIndex stays 0..Count-1 with no gaps.
        // A removed counter step gives its counter back. Returns false (no change) when out of range.
        public bool RemoveAt(int turnIndex)
        {
            if (turnIndex < 0 || turnIndex >= steps.Count)
                return false;

            if (steps[turnIndex].IsCounter)
                counterCount--;
            steps.RemoveAt(turnIndex);

            for (int i = turnIndex; i < steps.Count; i++)
            {
                TechniqueStep step = steps[i];
                steps[i] = new TechniqueStep(i, step.IsCounter, step.Stroke, step.RawPoints);
            }

            return true;
        }

        // Clears all steps and restores the counter budget so a fresh sequence can be built.
        public void Reset()
        {
            steps.Clear();
            counterCount = 0;
        }
    }
}
