namespace Game.Sim.Battle
{
    public static class BattleConstants
    {
        public const int StartingHP = 100;
        public const int SlashDamage = 10;
        public const int CounterBonusDamage = 20;

        // Thrust damage = Thrust area (in whole sim units, i.e. canvas px^2) / this divisor.
        public const int ThrustDamageDivisor = 1000;
    }
}
