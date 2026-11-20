using System.Collections.Generic;

namespace Game.Sim.Battle
{
    public class BattleResult
    {
        public List<TurnEvent> Events { get; }
        public int FinalHpA { get; }
        public int FinalHpB { get; }
        public BattleOutcome Outcome { get; }
        // HP each side started with (its 내공; see CombatPowerRules.MaxHp).
        public int MaxHpA { get; }
        public int MaxHpB { get; }

        public BattleResult(List<TurnEvent> events, int finalHpA, int finalHpB, BattleOutcome outcome)
            : this(events, finalHpA, finalHpB, outcome, BattleConstants.StartingHP, BattleConstants.StartingHP)
        {
        }

        public BattleResult(List<TurnEvent> events, int finalHpA, int finalHpB, BattleOutcome outcome, int maxHpA, int maxHpB)
        {
            Events = events;
            FinalHpA = finalHpA;
            FinalHpB = finalHpB;
            Outcome = outcome;
            MaxHpA = maxHpA;
            MaxHpB = maxHpB;
        }
    }
}
