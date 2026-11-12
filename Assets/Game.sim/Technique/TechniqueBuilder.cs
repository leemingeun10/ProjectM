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

        // 내공 this sequence's hidden-technique turns may spend in total: the player's trained
        // InternalPower. A building budget, not a cost taken from the save. Spent is worked out from
        // the steps, so removing a hidden turn gives its cost back. Kept across Reset.
        public int InternalPower { get; set; }
        public int SpentInternalPower => HiddenTechniques.TotalCost(steps);
        public int RemainingInternalPower => InternalPower - SpentInternalPower;

        public TechniqueAddResult TryAddStep(bool isCounter, StrokeResult stroke, IReadOnlyList<StrokePoint> rawPoints)
            => TryAddStep(isCounter, stroke, rawPoints, null, HiddenTechniqueMove.None);

        // A hidden-technique turn is refused (nothing added) when the combination is invalid or the
        // remaining 내공 can't pay for it; the result says which, so the caller can save the shape
        // as an ordinary turn instead.
        public TechniqueAddResult TryAddStep(bool isCounter, StrokeResult stroke, IReadOnlyList<StrokePoint> rawPoints,
            string hiddenTechniqueId, HiddenTechniqueMove hiddenMove)
        {
            if (steps.Count >= MaxTurns)
                return TechniqueAddResult.SequenceFull;

            if (isCounter && counterCount >= MaxCounters)
                return TechniqueAddResult.CounterLimitReached;

            var step = new TechniqueStep(steps.Count, isCounter, stroke, rawPoints, hiddenTechniqueId, hiddenMove);
            TechniqueAddResult hidden = HiddenTechniques.Check(step);
            if (hidden != TechniqueAddResult.Success)
                return hidden;
            if (HiddenTechniques.CostOf(step) > RemainingInternalPower)
                return TechniqueAddResult.NotEnoughInternalPower;

            steps.Add(step);
            if (isCounter)
                counterCount++;

            return TechniqueAddResult.Success;
        }

        // Removes one turn and renumbers the later ones so TurnIndex stays 0..Count-1 with no gaps.
        // A removed counter step gives its counter back (and a hidden one its 내공, via
        // SpentInternalPower). Returns false (no change) when out of range.
        public bool RemoveAt(int turnIndex)
        {
            if (turnIndex < 0 || turnIndex >= steps.Count)
                return false;

            if (steps[turnIndex].IsCounter)
                counterCount--;
            steps.RemoveAt(turnIndex);

            for (int i = turnIndex; i < steps.Count; i++)
                steps[i] = steps[i].WithTurnIndex(i);

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
