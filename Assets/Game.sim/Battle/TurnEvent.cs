namespace Game.Sim.Battle
{
    public readonly struct TurnEvent
    {
        public readonly int TurnIndex;
        public readonly BattleStepType TypeA;
        public readonly BattleStepType TypeB;
        public readonly int DamageToA;
        public readonly int DamageToB;
        public readonly TurnReason Reason;
        public readonly int HpAAfter;
        public readonly int HpBAfter;

        public TurnEvent(int turnIndex, BattleStepType typeA, BattleStepType typeB, int damageToA, int damageToB,
            TurnReason reason, int hpAAfter, int hpBAfter)
        {
            TurnIndex = turnIndex;
            TypeA = typeA;
            TypeB = typeB;
            DamageToA = damageToA;
            DamageToB = damageToB;
            Reason = reason;
            HpAAfter = hpAAfter;
            HpBAfter = hpBAfter;
        }

        public override string ToString()
            => $"Turn {TurnIndex}: {TypeA} vs {TypeB} ({Reason}) dmgA={DamageToA} dmgB={DamageToB} hpA={HpAAfter} hpB={HpBAfter}";
    }
}
