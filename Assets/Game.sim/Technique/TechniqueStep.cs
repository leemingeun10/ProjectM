using System.Collections.Generic;
using Game.Sim.Stroke;

namespace Game.Sim.Technique
{
    public readonly struct TechniqueStep
    {
        public readonly int TurnIndex;
        public readonly Stance Stance;
        public readonly bool IsCounter;
        public readonly StrokeResult Stroke;
        public readonly IReadOnlyList<StrokePoint> RawPoints;

        public TechniqueStep(int turnIndex, Stance stance, bool isCounter, StrokeResult stroke, IReadOnlyList<StrokePoint> rawPoints)
        {
            TurnIndex = turnIndex;
            Stance = stance;
            IsCounter = isCounter;
            Stroke = stroke;

            // Defensive copy: the caller's list (e.g. a reused buffer in Game.Client) must not
            // be able to mutate a step after it has been recorded.
            int count = rawPoints?.Count ?? 0;
            var copy = new StrokePoint[count];
            for (int i = 0; i < count; i++)
                copy[i] = rawPoints[i];
            RawPoints = copy;
        }
    }
}
