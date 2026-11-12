using System.Collections.Generic;
using Game.Sim.Stroke;

namespace Game.Sim.Technique
{
    public readonly struct TechniqueStep
    {
        public readonly int TurnIndex;
        public readonly bool IsCounter;
        public readonly StrokeResult Stroke;
        public readonly IReadOnlyList<StrokePoint> RawPoints;
        // Hidden technique this turn is saved as (HiddenTechniques id), or null for an ordinary
        // turn. A hidden turn always has a HiddenMove, which overrides Stroke.Type in battle; its
        // RawPoints (the drawn shape) are still what contact and area are judged on.
        public readonly string HiddenTechniqueId;
        public readonly HiddenTechniqueMove HiddenMove;

        public TechniqueStep(int turnIndex, bool isCounter, StrokeResult stroke, IReadOnlyList<StrokePoint> rawPoints,
            string hiddenTechniqueId = null, HiddenTechniqueMove hiddenMove = HiddenTechniqueMove.None)
        {
            TurnIndex = turnIndex;
            IsCounter = isCounter;
            Stroke = stroke;
            HiddenTechniqueId = string.IsNullOrEmpty(hiddenTechniqueId) ? null : hiddenTechniqueId;
            HiddenMove = hiddenMove;

            // Defensive copy: the caller's list (e.g. a reused buffer in Game.Client) must not
            // be able to mutate a step after it has been recorded.
            int count = rawPoints?.Count ?? 0;
            var copy = new StrokePoint[count];
            for (int i = 0; i < count; i++)
                copy[i] = rawPoints[i];
            RawPoints = copy;
        }

        public bool IsHidden => HiddenTechniqueId != null;

        public TechniqueStep WithTurnIndex(int turnIndex)
            => new TechniqueStep(turnIndex, IsCounter, Stroke, RawPoints, HiddenTechniqueId, HiddenMove);
    }
}
