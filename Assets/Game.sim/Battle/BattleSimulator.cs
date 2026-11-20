using System;
using System.Collections.Generic;
using Game.Sim.Common;
using Game.Sim.Stroke;
using Game.Sim.Technique;

namespace Game.Sim.Battle
{
    // Resolves two 10-turn technique sequences against each other, turn i vs turn i.
    // Pure function of its inputs: no RNG, no clock, integer/Fixed math only.
    //
    // A hidden-technique turn fights as its chosen move (GetStepType) on its drawn points, and
    // whatever damage it lands is scaled by its bonus (HiddenTechniques.DamagePercentOf). Matchups
    // and their outcomes are otherwise exactly those of ordinary turns.
    //
    // Each side fights with its CombatStats: 내공 is its maximum HP and 외공 scales the damage it
    // deals (CombatPowerRules). Without stats both sides are 100 / 100, where damage and HP are the
    // turn table's own numbers.
    public static class BattleSimulator
    {
        public const int TurnsPerBattle = TechniqueBuilder.MaxTurns;

        public static BattleResult Simulate(IReadOnlyList<TechniqueStep> stepsA, IReadOnlyList<TechniqueStep> stepsB)
            => Simulate(stepsA, stepsB, CombatStats.Default, CombatStats.Default);

        public static BattleResult Simulate(IReadOnlyList<TechniqueStep> stepsA, IReadOnlyList<TechniqueStep> stepsB,
            CombatStats statsA, CombatStats statsB)
        {
            Validate(stepsA, nameof(stepsA));
            Validate(stepsB, nameof(stepsB));
            Validate(statsA, nameof(statsA));
            Validate(statsB, nameof(statsB));

            int maxHpA = CombatPowerRules.MaxHp(statsA);
            int maxHpB = CombatPowerRules.MaxHp(statsB);
            int hpA = maxHpA;
            int hpB = maxHpB;
            var events = new List<TurnEvent>(TurnsPerBattle);

            for (int turn = 0; turn < TurnsPerBattle; turn++)
            {
                TechniqueStep a = stepsA[turn];
                TechniqueStep b = stepsB[turn];
                BattleStepType typeA = GetStepType(a);
                BattleStepType typeB = GetStepType(b);

                (int damageToA, int damageToB, TurnReason reason) = ResolveTurn(typeA, a, typeB, b);
                // Damage to one side is always dealt by the other side's turn, so it takes that
                // side's hidden-technique bonus and 외공.
                damageToA = CombatPowerRules.Damage(damageToA, HiddenTechniques.DamagePercentOf(b), statsB.ExternalPower);
                damageToB = CombatPowerRules.Damage(damageToB, HiddenTechniques.DamagePercentOf(a), statsA.ExternalPower);
                hpA -= damageToA;
                hpB -= damageToB;
                events.Add(new TurnEvent(turn, typeA, typeB, damageToA, damageToB, reason, hpA, hpB));

                if (hpA <= 0 || hpB <= 0)
                    break;
            }

            return new BattleResult(events, hpA, hpB, DecideOutcome(hpA, maxHpA, hpB, maxHpB), maxHpA, maxHpB);
        }

        public static BattleStepType GetStepType(TechniqueStep step)
        {
            if (step.IsCounter)
                return BattleStepType.Counter;
            // The chosen move overrides the analyzer: a triangle is closed, so it always analyses
            // as a Thrust, but a hidden turn saved as a cut fights as a Slash.
            if (step.IsHidden)
                return step.HiddenMove == HiddenTechniqueMove.Thrust ? BattleStepType.Thrust : BattleStepType.Slash;
            return step.Stroke.Type == StrokeType.Thrust ? BattleStepType.Thrust : BattleStepType.Slash;
        }

        // Base damage of a turn fighting as a Thrust, before any hidden-technique bonus.
        public static int ThrustDamage(TechniqueStep thrustStep)
        {
            // A hidden Thrust whose shape didn't close (analysed as a Slash, so no stored area) is
            // measured as the closed polygon of its points instead.
            Fixed areaFixed = thrustStep.Stroke.Type == StrokeType.Thrust
                ? thrustStep.Stroke.Area
                : StrokeAnalyzer.ClosedArea(thrustStep.RawPoints);
            // Area is in whole sim units (canvas px^2); Raw carries Fixed.Scale extra digits.
            long area = areaFixed.Raw / Fixed.Scale;
            long damage = IntegerMath.IntegerSqrt(area) / BattleConstants.ThrustSqrtDivisor;
            return damage > int.MaxValue ? int.MaxValue : (int)damage;
        }

        // Both KO'd on the same turn counts as a draw, and a lone KO loses. If both are still
        // standing after the last turn, the side with the larger share of its HP left wins
        // (remaining / maximum), so a large HP pool alone does not win on time.
        private static BattleOutcome DecideOutcome(int hpA, int maxHpA, int hpB, int maxHpB)
        {
            bool aDown = hpA <= 0;
            bool bDown = hpB <= 0;
            if (aDown && bDown) return BattleOutcome.Draw;
            if (bDown) return BattleOutcome.WinA;
            if (aDown) return BattleOutcome.WinB;
            int share = CombatPowerRules.CompareHpShare(hpA, maxHpA, hpB, maxHpB);
            if (share > 0) return BattleOutcome.WinA;
            if (share < 0) return BattleOutcome.WinB;
            return BattleOutcome.Draw;
        }

        // Each matchup is written once in a canonical order; the mirrored order is handled by
        // swapping sides and swapping the resulting damage back.
        private static (int damageToA, int damageToB, TurnReason reason) ResolveTurn(
            BattleStepType typeA, TechniqueStep a, BattleStepType typeB, TechniqueStep b)
        {
            switch (typeA, typeB)
            {
                case (BattleStepType.Slash, BattleStepType.Slash):
                    return StrokeAnalyzer.CrossStrokeContact(a.RawPoints, b.RawPoints)
                        ? (0, 0, TurnReason.CrossContact)
                        : (BattleConstants.SlashDamage, BattleConstants.SlashDamage, TurnReason.Miss);

                case (BattleStepType.Counter, BattleStepType.Slash):
                    return StrokeAnalyzer.CrossStrokeContact(a.RawPoints, b.RawPoints)
                        ? (0, BattleConstants.CounterBonusDamage, TurnReason.CrossContact)
                        : (BattleConstants.SlashDamage, 0, TurnReason.Miss);

                case (BattleStepType.Thrust, BattleStepType.Slash):
                    return StrokeAnalyzer.CrossStrokeContact(b.RawPoints, a.RawPoints)
                        ? (BattleConstants.SlashDamage, 0, TurnReason.CrossContact)
                        : (0, ThrustDamage(a), TurnReason.Miss);

                case (BattleStepType.Thrust, BattleStepType.Thrust):
                    return (ThrustDamage(b), ThrustDamage(a), TurnReason.Unconditional);

                case (BattleStepType.Thrust, BattleStepType.Counter):
                    return (0, ThrustDamage(a), TurnReason.Unconditional);

                case (BattleStepType.Counter, BattleStepType.Counter):
                    return (0, 0, TurnReason.Unconditional);

                default:
                    (int toB, int toA, TurnReason reason) = ResolveTurn(typeB, b, typeA, a);
                    return (toA, toB, reason);
            }
        }

        // default(CombatStats) skips the constructor's checks; 0 HP would end the battle unfought.
        private static void Validate(CombatStats stats, string paramName)
        {
            if (stats.InternalPower < 1 || stats.ExternalPower < 1)
                throw new ArgumentException($"Combat stats must both be at least 1 (got {stats}); use CombatStats.Default for none.", paramName);
        }

        private static void Validate(IReadOnlyList<TechniqueStep> steps, string paramName)
        {
            if (steps == null)
                throw new ArgumentNullException(paramName);
            if (steps.Count != TurnsPerBattle)
                throw new ArgumentException($"A battle sequence must have exactly {TurnsPerBattle} steps (got {steps.Count}).", paramName);
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i].TurnIndex != i)
                    throw new ArgumentException($"Step at position {i} has TurnIndex {steps[i].TurnIndex}; steps must be ordered by turn.", paramName);
                TechniqueAddResult hidden = HiddenTechniques.Check(steps[i]);
                if (hidden != TechniqueAddResult.Success)
                    throw new ArgumentException($"Step at position {i} has an invalid hidden technique ({hidden}).", paramName);
            }
        }
    }
}
