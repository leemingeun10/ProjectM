using System;
using System.Collections.Generic;
using Game.Sim.Shape;

namespace Game.Sim.Cultivation
{
    // How the cultivation powers apply when drawing a technique: 외공 lengthens the stroke a turn
    // may draw, 내공 pays for hidden techniques recognised from the drawn shape.
    public static class CultivationBattleRules
    {
        // Stroke length limit in centimetres (the unit of StrokeResult.Length) at 외공 0 and at
        // StrokeLengthFullPower and above, linear and rounded down in between: 0-49 -> 10, 50-99 -> 11.
        public const int BaseStrokeLength = 10;
        public const int FullStrokeLength = 12;
        public const int StrokeLengthFullPower = 100;

        // 내공 each built-in hidden technique costs, keyed by ShapeTemplate.Id.
        public static readonly IReadOnlyDictionary<string, int> HiddenTechniqueCosts = new Dictionary<string, int>
        {
            { ShapeTemplates.TriangleId, 30 }, // 삼재검법
            { ShapeTemplates.FlowerId, 30 },   // 매화검법
            { ShapeTemplates.SwordId, 30 },    // 제왕검법 (placeholder cost, to be tuned)
            { ShapeTemplates.CrescentId, 30 }, // 월영검법 (placeholder cost, to be tuned)
        };

        public static int MaxStrokeLength(int externalPower)
        {
            int power = Math.Max(0, Math.Min(externalPower, StrokeLengthFullPower));
            return BaseStrokeLength + (FullStrokeLength - BaseStrokeLength) * power / StrokeLengthFullPower;
        }

        // Spends the matched template's cost from currentInternalPower. Not activated (power
        // unchanged) when the power is short or the template has no listed cost.
        public static (bool activated, int remainingInternalPower) TryActivateHiddenTechnique(
            int currentInternalPower, ShapeMatchResult match, IReadOnlyDictionary<string, int> costByTemplateId)
        {
            if (costByTemplateId == null)
                throw new ArgumentNullException(nameof(costByTemplateId));

            if (match.TemplateId == null
                || !costByTemplateId.TryGetValue(match.TemplateId, out int cost)
                || currentInternalPower < cost)
                return (false, currentInternalPower);

            return (true, currentInternalPower - cost);
        }
    }
}
