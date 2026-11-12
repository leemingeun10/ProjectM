using System.Collections.Generic;
using Game.Sim.Shape;
using Game.Sim.Technique;

namespace Game.Client.Technique
{
    // Display names of the hidden techniques, keyed by the ShapeTemplate.Id they are drawn with,
    // and of the moves a hidden turn can be saved with.
    public static class HiddenTechniqueNames
    {
        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            { ShapeTemplates.TriangleId, "삼재검법" },
            { ShapeTemplates.FlowerId, "매화검법" },
            { ShapeTemplates.SwordId, "제왕검법" },
            { ShapeTemplates.CrescentId, "월영검법" },
        };

        public static string Of(string templateId)
            => templateId != null && Names.TryGetValue(templateId, out string name) ? name : templateId;

        public static string MoveName(HiddenTechniqueMove move)
        {
            switch (move)
            {
                case HiddenTechniqueMove.HorizontalSlash: return "가로베기";
                case HiddenTechniqueMove.VerticalSlash: return "세로베기";
                case HiddenTechniqueMove.Thrust: return "찌르기";
                default: return string.Empty;
            }
        }

        // "삼재검법·가로베기" for a hidden step, null for an ordinary one.
        public static string OfStep(TechniqueStep step)
            => step.IsHidden ? $"{Of(step.HiddenTechniqueId)}·{MoveName(step.HiddenMove)}" : null;
    }
}
