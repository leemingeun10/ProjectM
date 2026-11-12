using Game.Sim.Shape;
using UnityEngine;

namespace Game.Client.Battle
{
    // Colour of the qi (기운) a hidden technique's turns give off from the sword (SwordQiEffect),
    // keyed by HiddenTechniques id. A new technique gets an entry here.
    public static class HiddenTechniqueQiColors
    {
        // 삼재검법: light yellow-green.
        public static readonly Color Samjae = new Color(0.62f, 1f, 0.45f);

        public static bool TryGet(string hiddenTechniqueId, out Color color)
        {
            switch (hiddenTechniqueId)
            {
                case ShapeTemplates.TriangleId: color = Samjae; return true;
                default: color = default; return false;
            }
        }
    }
}
