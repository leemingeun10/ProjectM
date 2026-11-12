using System.Collections.Generic;

namespace Game.Client.Battle
{
    // One side's technique designation for one replayed turn: "on turn TurnIndex this side performs
    // technique TechniqueId at Stage". Client-only presentation data: it picks the clip and the turn's
    // length in the replay and never reaches Game.sim, the battle result or the saved step format.
    //
    // Nothing fills these yet: they are for future techniques with their own long clips
    // (TechniqueClipCatalog). 삼재검법 is saved on the step itself (TechniqueStep.HiddenTechniqueId)
    // and needs none.
    public readonly struct TechniqueCast
    {
        public readonly int TurnIndex;
        // ShapeTemplate id the technique is drawn with (ShapeTemplates.TriangleId, ...).
        public readonly string TechniqueId;
        // 1-based.
        public readonly int Stage;

        public TechniqueCast(int turnIndex, string techniqueId, int stage)
        {
            TurnIndex = turnIndex;
            TechniqueId = techniqueId;
            Stage = stage;
        }

        // The cast for turnIndex, if any; when several share a turn the first one wins.
        public static TechniqueCast? Find(IReadOnlyList<TechniqueCast> casts, int turnIndex)
        {
            if (casts == null)
                return null;
            foreach (TechniqueCast cast in casts)
            {
                if (cast.TurnIndex == turnIndex)
                    return cast;
            }
            return null;
        }
    }
}
