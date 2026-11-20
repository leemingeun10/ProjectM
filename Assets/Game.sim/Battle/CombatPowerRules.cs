using System;
using Game.Sim.Common;
using Game.Sim.Cultivation;

namespace Game.Sim.Battle
{
    // 전투력 (combat power) and everything derived from the two combat stats.
    //   combat stat   = BaseStat + trained 단련 value (내공 and 외공 separately)
    //   combat power  = 내공 * 외공 / 10000, handled as permille (1000 = power 1)
    //   damage dealt  = base damage * hidden-technique percent * attacker's 외공 / 100 * x
    //   maximum HP    = 내공
    // Integer only. Every formula multiplies first and divides once, and every division here
    // rounds half up (RoundedDivide), so the results are the same on every device.
    public static class CombatPowerRules
    {
        // Both stats of a character who has never trained.
        public const int BaseStat = 100;

        // "x": global damage multiplier in permille (1000 = x1). Raising it shortens battles
        // without changing who wins much; see CombatPowerFormulaSimulation.
        public const int DamageScalePermille = 1000;

        public const int PowerPermilleScale = 1000;
        // 내공 * 외공 at combat power 1 (BaseStat * BaseStat).
        private const int StatProductPerPower = BaseStat * BaseStat;
        private const int PercentBase = 100;

        // Enemy power = a stage's recommended power / this divisor (in tenths: 18 = 1.8), so a
        // player at the recommended power is the stronger side. The same for every AI difficulty.
        public const int EnemyPowerDivisorTenths = 18;
        private const int TenthsScale = 10;

        // The stats a player fights with: BaseStat plus what they trained.
        public static CombatStats FromCultivation(CultivationStats stats)
            => new CombatStats(checked(BaseStat + stats.InternalPower), checked(BaseStat + stats.ExternalPower));

        // 내공 * 외공 / 10000 in permille: 100 / 100 -> 1000, 75 / 75 -> 563 (0.5625 rounded).
        public static int PowerPermille(CombatStats stats)
            => ToInt(RoundedDivide((long)stats.InternalPower * stats.ExternalPower * PowerPermilleScale, StatProductPerPower));

        public static int MaxHp(CombatStats stats) => stats.InternalPower;

        // Damage one hit deals. baseDamage is the turn table's figure (slash, counter or thrust),
        // hiddenDamagePercent the attacker's hidden-technique bonus (100 for an ordinary turn).
        // With 외공 100 this is exactly the pre-stat damage.
        public static int Damage(int baseDamage, int hiddenDamagePercent, int attackerExternalPower)
        {
            if (baseDamage <= 0)
                return 0;
            long scaled = RoundedDivide(
                (long)baseDamage * hiddenDamagePercent * attackerExternalPower * DamageScalePermille,
                (long)PercentBase * BaseStat * PowerPermilleScale);
            return ToInt(scaled);
        }

        // Who has the larger share of their HP left, compared as hpA / maxHpA vs hpB / maxHpB by
        // cross multiplication. > 0: A, < 0: B, 0: equal.
        public static int CompareHpShare(int hpA, int maxHpA, int hpB, int maxHpB)
        {
            long left = (long)hpA * maxHpB;
            long right = (long)hpB * maxHpA;
            return left.CompareTo(right);
        }

        // Stats of the enemy of a stage with this recommended power. 내공 = 외공 =
        // round(100 * sqrt(recommended / divisor)): recommended 1.0 is power 0.56, so 75 / 75.
        public static CombatStats EnemyStats(int recommendedPowerPermille)
        {
            if (recommendedPowerPermille < 1)
                throw new ArgumentOutOfRangeException(nameof(recommendedPowerPermille), recommendedPowerPermille, "Recommended power must be positive.");
            // stat^2 = 10000 * power = 10000 * recommended * 10 / (1000 * divisorTenths)
            int stat = RoundedSqrtOfRatio((long)StatProductPerPower * recommendedPowerPermille * TenthsScale,
                (long)PowerPermilleScale * EnemyPowerDivisorTenths);
            return new CombatStats(stat, stat);
        }

        // Stats of an enemy whose power is given directly: 내공 = 외공 = round(100 * sqrt(power)).
        public static CombatStats StatsForPower(int powerPermille)
        {
            if (powerPermille < 1)
                throw new ArgumentOutOfRangeException(nameof(powerPermille), powerPermille, "Power must be positive.");
            int stat = RoundedSqrtOfRatio((long)StatProductPerPower * powerPermille, PowerPermilleScale);
            return new CombatStats(stat, stat);
        }

        // round(sqrt(numerator / denominator)), half up, at least 1. The nearest integer s satisfies
        // (2s - 1)^2 <= 4n / d < (2s + 1)^2, so s = (floor(sqrt(4n / d)) + 1) / 2. Flooring 4n / d
        // first is exact: it drops only a fraction, which cannot change a comparison with the
        // integer (2s - 1)^2.
        private static int RoundedSqrtOfRatio(long numerator, long denominator)
        {
            long s = (IntegerMath.IntegerSqrt(4 * numerator / denominator) + 1) / 2;
            return ToInt(Math.Max(1, s));
        }

        // The one rounding rule: half up, for non-negative values.
        private static long RoundedDivide(long numerator, long denominator) => (numerator + denominator / 2) / denominator;

        private static int ToInt(long value) => value > int.MaxValue ? int.MaxValue : (int)value;
    }
}
