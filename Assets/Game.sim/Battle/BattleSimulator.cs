using System;
using System.Collections.Generic;
using Game.Sim.Stroke;
using Game.Sim.Technique;

namespace Game.Sim.Battle
{
    // Resolves two 10-turn technique sequences against each other, turn i vs turn i.
    // Pure function of its inputs: no RNG, no clock, integer/Fixed math only.
    public static class BattleSimulator
    {
        public const int TurnsPerBattle = TechniqueBuilder.MaxTurns;

        public static BattleResult Simulate(IReadOnlyList<TechniqueStep> stepsA, IReadOnlyList<TechniqueStep> stepsB)
        {
            Validate(stepsA, nameof(stepsA));
            Validate(stepsB, nameof(stepsB));

            int hpA = BattleConstants.StartingHP;
            int hpB = BattleConstants.StartingHP;
            var events = new List<TurnEvent>(TurnsPerBattle);

            for (int turn = 0; turn < TurnsPerBattle; turn++)
            {
                TechniqueStep a = stepsA[turn];
                TechniqueStep b = stepsB[turn];
                BattleStepType typeA = GetStepType(a);
                BattleStepType typeB = GetStepType(b);

                (int damageToA, int damageToB, TurnReason reason) = ResolveTurn(typeA, a, typeB, b);
                hpA -= damageToA;
                hpB -= damageToB;
                events.Add(new TurnEvent(turn, typeA, typeB, damageToA, damageToB, reason, hpA, hpB));

                if (hpA <= 0 || hpB <= 0)
                    break;
            }

            return new BattleResult(events, hpA, hpB, DecideOutcome(hpA, hpB));
        }

        public static BattleStepType GetStepType(TechniqueStep step)
        {
            if (step.IsCounter)
                return BattleStepType.Counter;
            return step.Stroke.Type == StrokeType.Thrust ? BattleStepType.Thrust : BattleStepType.Slash;
        }

        public static int ThrustDamage(TechniqueStep thrustStep)
        {
            // Area is in whole sim units (canvas px^2); Raw carries Fixed.Scale extra digits.
            long damage = thrustStep.Stroke.Area.Raw / (Fixed.Scale * BattleConstants.ThrustDamageDivisor);
            return damage > int.MaxValue ? int.MaxValue : (int)damage;
        }

        // Both KO'd on the same turn counts as a draw; otherwise the survivor / higher HP wins.
        private static BattleOutcome DecideOutcome(int hpA, int hpB)
        {
            bool aDown = hpA <= 0;
            bool bDown = hpB <= 0;
            if (aDown && bDown) return BattleOutcome.Draw;
            if (bDown) return BattleOutcome.WinA;
            if (aDown) return BattleOutcome.WinB;
            if (hpA > hpB) return BattleOutcome.WinA;
            if (hpB > hpA) return BattleOutcome.WinB;
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
            }
        }
    }
}
