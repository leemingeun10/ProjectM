using System.Collections.Generic;
using Game.Sim.Shape;

namespace Game.Client.Technique
{
    // Display names of the hidden techniques, keyed by the ShapeTemplate.Id they are drawn with.
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
    }
}
