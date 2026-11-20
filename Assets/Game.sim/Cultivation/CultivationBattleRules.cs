using Game.Sim.Stroke;

namespace Game.Sim.Cultivation
{
    // How the cultivation powers apply when drawing a technique. 내공 pays for hidden techniques:
    // their costs live in HiddenTechniques and the per-sequence budget in TechniqueBuilder
    // (InternalPower / RemainingInternalPower). 외공 does not change what can be drawn: the stroke
    // length limit is the same for every character, whatever their powers.
    public static class CultivationBattleRules
    {
        // Path length one turn's stroke may have, in cm * Fixed.Scale: frame width * 8 / 3, so
        // 12 cm for the 4.5 cm frame. Defined with the frame (StrokeUnits) so it follows the
        // frame's size.
        public const long MaxStrokeLengthCmRaw = StrokeUnits.MaxStrokeLengthCmRaw;
    }
}
