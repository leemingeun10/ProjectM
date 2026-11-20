using System;

namespace Game.Sim.Battle
{
    // The two stats one side fights with. 내공 (InternalPower) is its maximum HP, 외공
    // (ExternalPower) scales the damage it deals; see CombatPowerRules. default(CombatStats) is
    // not a valid fighter: use Default (100 / 100) for "no stats given".
    public readonly struct CombatStats
    {
        public readonly int InternalPower;
        public readonly int ExternalPower;

        public CombatStats(int internalPower, int externalPower)
        {
            if (internalPower < 1)
                throw new ArgumentOutOfRangeException(nameof(internalPower), internalPower, "내공 must be at least 1.");
            if (externalPower < 1)
                throw new ArgumentOutOfRangeException(nameof(externalPower), externalPower, "외공 must be at least 1.");
            InternalPower = internalPower;
            ExternalPower = externalPower;
        }

        // A character with no training, and both sides of a 비무: combat power 1.
        public static CombatStats Default => new CombatStats(CombatPowerRules.BaseStat, CombatPowerRules.BaseStat);

        public override string ToString() => $"내공 {InternalPower} · 외공 {ExternalPower}";
    }
}
