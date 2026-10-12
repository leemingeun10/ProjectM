using Game.Sim.Battle;

namespace Game.Client.Battle
{
    // Turns one already-resolved TurnEvent into a short Korean line for the replay, always from
    // the player's (side A's) point of view: "{n}턴 · {A} vs {B} · {what happened} · {damage}".
    // Only TurnEvent fields are used. Which rule fired is fully identified by the two step types
    // plus Reason (see TurnReason); the damage figures are read back, never recomputed.
    public static class TurnNarrator
    {
        private const string ThrustNote = "(원 크기 비례)";

        public static string Describe(TurnEvent e)
            => $"{e.TurnIndex + 1}턴 · {DescribeStepType(e.TypeA)} vs {DescribeStepType(e.TypeB)} · {Explain(e)}{KnockOut(e)}";

        public static string DescribeStepType(BattleStepType type)
        {
            switch (type)
            {
                case BattleStepType.Slash: return "베기";
                case BattleStepType.Thrust: return "찌르기";
                case BattleStepType.Counter: return "카운터";
                default: return type.ToString();
            }
        }

        private static string Explain(TurnEvent e)
        {
            bool contact = e.Reason == TurnReason.CrossContact;
            bool contactChecked = e.Reason != TurnReason.Unconditional;
            switch ((e.TypeA, e.TypeB, contactChecked))
            {
                case (BattleStepType.Slash, BattleStepType.Slash, true):
                    return contact ? "검로가 교차해 서로 막아냄 · 피해 없음" : $"검로가 엇갈려 서로 베임 · {Mutual(e, false)}";

                case (BattleStepType.Counter, BattleStepType.Slash, true):
                    return contact ? $"카운터 성공 · 상대에게 {e.DamageToB} 피해" : $"카운터 시도 실패 · 그대로 베기 {e.DamageToA} 피해를 입음";

                case (BattleStepType.Slash, BattleStepType.Counter, true):
                    return contact ? $"상대 카운터에 걸림 · {e.DamageToA} 피해를 입음" : $"상대 카운터가 빗나감 · 상대에게 베기 {e.DamageToB} 피해";

                case (BattleStepType.Thrust, BattleStepType.Slash, true):
                    return contact ? $"상대 베기가 찌르기를 끊음 · {e.DamageToA} 피해를 입음" : $"상대 베기가 빗나가 찌르기 적중 · {ThrustHit(e.DamageToB, true)}";

                case (BattleStepType.Slash, BattleStepType.Thrust, true):
                    return contact ? $"베기로 상대 찌르기를 끊음 · 상대에게 {e.DamageToB} 피해" : $"베기가 빗나가 상대 찌르기 적중 · {ThrustHit(e.DamageToA, false)}";

                case (BattleStepType.Thrust, BattleStepType.Thrust, false):
                    return $"서로 찌르기 적중 · {Mutual(e, true)}";

                case (BattleStepType.Thrust, BattleStepType.Counter, false):
                    return $"카운터로는 찌르기를 막지 못함 · {ThrustHit(e.DamageToB, true)}";

                case (BattleStepType.Counter, BattleStepType.Thrust, false):
                    return $"카운터로는 찌르기를 막지 못함 · {ThrustHit(e.DamageToA, false)}";

                case (BattleStepType.Counter, BattleStepType.Counter, false):
                    return "서로 카운터 자세 · 피해 없음";

                // Not a combination BattleSimulator produces; still say only what the event holds.
                default:
                    return $"{DescribeReason(e.Reason)} · {Mutual(e, false)}";
            }
        }

        // A landed thrust whose damage is 0 had too small a loop: ThrustDamage is sqrt(area) / divisor.
        private static string ThrustHit(int damage, bool toOpponent)
        {
            if (damage <= 0)
                return "원이 작아 피해 없음";
            return toOpponent ? $"상대에게 찌르기 {damage} 피해{ThrustNote}" : $"찌르기 {damage} 피해를 입음{ThrustNote}";
        }

        private static string Mutual(TurnEvent e, bool thrust)
        {
            string note = thrust ? ThrustNote : string.Empty;
            int toA = e.DamageToA;
            int toB = e.DamageToB;
            if (toA <= 0 && toB <= 0) return "피해 없음";
            if (toA <= 0) return $"상대에게 {toB} 피해{note}";
            if (toB <= 0) return $"{toA} 피해를 입음{note}";
            if (toA == toB) return $"서로 {toA} 피해{note}";
            return $"나 {toA} · 상대 {toB} 피해{note}";
        }

        private static string KnockOut(TurnEvent e)
        {
            bool aDown = e.HpAAfter <= 0;
            bool bDown = e.HpBAfter <= 0;
            if (aDown && bDown) return " · 함께 쓰러짐";
            if (bDown) return " · 상대 쓰러짐";
            if (aDown) return " · 쓰러짐";
            return string.Empty;
        }

        private static string DescribeReason(TurnReason reason)
        {
            switch (reason)
            {
                case TurnReason.CrossContact: return "접촉 성공";
                case TurnReason.Miss: return "접촉 실패";
                case TurnReason.Unconditional: return "무조건 판정";
                default: return reason.ToString();
            }
        }
    }
}
