namespace Game.Sim.Battle
{
    public static class BattleConstants
    {
        public const int StartingHP = 100;
        public const int SlashDamage = 10;
        public const int CounterBonusDamage = 20;

        // Thrust damage = Thrust area (in whole sim units, i.e. canvas px^2) / this divisor.
        // Calibrated so a radius-150 loop deals about SlashDamage: the 24-point loop the client
        // produces has area 12 * 150^2 * sin(15deg) ~= 69,882 -> 10 (a perfect circle, 70,686 -> 10).
        public const int ThrustDamageDivisor = 6900;
    }
}
