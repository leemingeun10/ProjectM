using System;

namespace Game.Sim.Cultivation
{
    // How the cultivation powers apply when drawing a technique: 외공 lengthens the stroke a turn
    // may draw. 내공 pays for hidden techniques: their costs live in HiddenTechniques and the
    // per-sequence budget in TechniqueBuilder (InternalPower / RemainingInternalPower).
    public static class CultivationBattleRules
    {
        // Stroke length limit in centimetres (the unit of StrokeResult.Length) at 외공 0 and at
        // StrokeLengthFullPower and above, linear and rounded down in between: 0-49 -> 10, 50-99 -> 11.
        public const int BaseStrokeLength = 10;
        public const int FullStrokeLength = 12;
        public const int StrokeLengthFullPower = 100;

        public static int MaxStrokeLength(int externalPower)
        {
            int power = Math.Max(0, Math.Min(externalPower, StrokeLengthFullPower));
            return BaseStrokeLength + (FullStrokeLength - BaseStrokeLength) * power / StrokeLengthFullPower;
        }
    }
}
