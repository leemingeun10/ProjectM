using System.Collections.Generic;

namespace Game.Sim.Battle
{
    public class BattleResult
    {
        public List<TurnEvent> Events { get; }
        public int FinalHpA { get; }
        public int FinalHpB { get; }
        public BattleOutcome Outcome { get; }

        public BattleResult(List<TurnEvent> events, int finalHpA, int finalHpB, BattleOutcome outcome)
        {
            Events = events;
            FinalHpA = finalHpA;
            FinalHpB = finalHpB;
            Outcome = outcome;
        }
    }
}
