namespace Game.Sim.Battle
{
    public static class BattleConstants
    {
        public const int StartingHP = 100;
        public const int SlashDamage = 10;
        public const int CounterBonusDamage = 20;

        // Thrust damage = IntegerSqrt(Thrust area in whole sim units, i.e. canvas px^2) / this divisor,
        // so damage grows with the loop's radius rather than its area.
        // Calibrated so a radius-150 loop deals SlashDamage: the 24-point loop the client produces
        // has area 12 * 150^2 * sin(15deg) ~= 69,882 -> sqrt 264 -> 264 / 26 = 10 (a perfect circle,
        // 70,686 -> 265 -> 10). 26 is the largest divisor that still gives 10, matching the old
        // linear divisor's choice of putting the 10-damage threshold just below radius 150 (~147.5).
        public const int ThrustSqrtDivisor = 26;
    }
}
