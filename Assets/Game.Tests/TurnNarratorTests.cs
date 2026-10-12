using Game.Client.Battle;
using Game.Sim.Battle;
using NUnit.Framework;

namespace Game.Tests
{
    public class TurnNarratorTests
    {
        private static TurnEvent Event(BattleStepType a, BattleStepType b, TurnReason reason, int toA, int toB,
            int hpA = 50, int hpB = 50, int turnIndex = 0)
            => new TurnEvent(turnIndex, a, b, toA, toB, reason, hpA, hpB);

        private const BattleStepType S = BattleStepType.Slash;
        private const BattleStepType T = BattleStepType.Thrust;
        private const BattleStepType C = BattleStepType.Counter;

        [Test]
        public void Prefix_HasOneBasedTurnAndBothTypes()
        {
            string text = TurnNarrator.Describe(Event(S, T, TurnReason.CrossContact, 0, 10, turnIndex: 2));
            StringAssert.StartsWith("3턴 · 베기 vs 찌르기 · ", text);
        }

        [Test]
        public void SlashSlash_Contact_NoDamage()
            => Assert.AreEqual("1턴 · 베기 vs 베기 · 검로가 교차해 서로 막아냄 · 피해 없음",
                TurnNarrator.Describe(Event(S, S, TurnReason.CrossContact, 0, 0)));

        [Test]
        public void SlashSlash_Miss_MutualDamage()
            => Assert.AreEqual("1턴 · 베기 vs 베기 · 검로가 엇갈려 서로 베임 · 서로 10 피해",
                TurnNarrator.Describe(Event(S, S, TurnReason.Miss, 10, 10)));

        [Test]
        public void PlayerCounter_SuccessAndFailure()
        {
            Assert.AreEqual("1턴 · 카운터 vs 베기 · 카운터 성공 · 상대에게 20 피해",
                TurnNarrator.Describe(Event(C, S, TurnReason.CrossContact, 0, 20)));
            Assert.AreEqual("1턴 · 카운터 vs 베기 · 카운터 시도 실패 · 그대로 베기 10 피해를 입음",
                TurnNarrator.Describe(Event(C, S, TurnReason.Miss, 10, 0)));
        }

        [Test]
        public void OpponentCounter_SuccessAndFailure()
        {
            Assert.AreEqual("1턴 · 베기 vs 카운터 · 상대 카운터에 걸림 · 20 피해를 입음",
                TurnNarrator.Describe(Event(S, C, TurnReason.CrossContact, 20, 0)));
            Assert.AreEqual("1턴 · 베기 vs 카운터 · 상대 카운터가 빗나감 · 상대에게 베기 10 피해",
                TurnNarrator.Describe(Event(S, C, TurnReason.Miss, 0, 10)));
        }

        [Test]
        public void ThrustVsSlash_BothSides()
        {
            Assert.AreEqual("1턴 · 찌르기 vs 베기 · 상대 베기가 찌르기를 끊음 · 10 피해를 입음",
                TurnNarrator.Describe(Event(T, S, TurnReason.CrossContact, 10, 0)));
            Assert.AreEqual("1턴 · 찌르기 vs 베기 · 상대 베기가 빗나가 찌르기 적중 · 상대에게 찌르기 12 피해(원 크기 비례)",
                TurnNarrator.Describe(Event(T, S, TurnReason.Miss, 0, 12)));
            Assert.AreEqual("1턴 · 베기 vs 찌르기 · 베기로 상대 찌르기를 끊음 · 상대에게 10 피해",
                TurnNarrator.Describe(Event(S, T, TurnReason.CrossContact, 0, 10)));
            Assert.AreEqual("1턴 · 베기 vs 찌르기 · 베기가 빗나가 상대 찌르기 적중 · 찌르기 7 피해를 입음(원 크기 비례)",
                TurnNarrator.Describe(Event(S, T, TurnReason.Miss, 7, 0)));
        }

        [Test]
        public void LandedThrust_WithZeroDamage_SaysLoopTooSmall()
            => Assert.AreEqual("1턴 · 찌르기 vs 카운터 · 카운터로는 찌르기를 막지 못함 · 원이 작아 피해 없음",
                TurnNarrator.Describe(Event(T, C, TurnReason.Unconditional, 0, 0)));

        [Test]
        public void ThrustThrust_UnequalDamage()
            => Assert.AreEqual("1턴 · 찌르기 vs 찌르기 · 서로 찌르기 적중 · 나 8 · 상대 12 피해(원 크기 비례)",
                TurnNarrator.Describe(Event(T, T, TurnReason.Unconditional, 8, 12)));

        [Test]
        public void CounterVsThrust_TakesThrustDamage()
            => Assert.AreEqual("1턴 · 카운터 vs 찌르기 · 카운터로는 찌르기를 막지 못함 · 찌르기 9 피해를 입음(원 크기 비례)",
                TurnNarrator.Describe(Event(C, T, TurnReason.Unconditional, 9, 0)));

        [Test]
        public void CounterCounter_NothingHappens()
            => Assert.AreEqual("1턴 · 카운터 vs 카운터 · 서로 카운터 자세 · 피해 없음",
                TurnNarrator.Describe(Event(C, C, TurnReason.Unconditional, 0, 0)));

        [Test]
        public void KnockOut_IsAppended()
        {
            StringAssert.EndsWith(" · 상대 쓰러짐", TurnNarrator.Describe(Event(C, S, TurnReason.CrossContact, 0, 20, hpB: 0)));
            StringAssert.EndsWith(" · 쓰러짐", TurnNarrator.Describe(Event(C, S, TurnReason.Miss, 10, 0, hpA: -5)));
            StringAssert.EndsWith(" · 함께 쓰러짐", TurnNarrator.Describe(Event(S, S, TurnReason.Miss, 10, 10, hpA: 0, hpB: 0)));
        }

        // Every event the simulator can actually produce gets a dedicated template, never the fallback.
        [Test]
        public void SimulatorEvents_NeverUseFallback()
        {
            var types = new[] { S, T, C };
            foreach (BattleStepType a in types)
            foreach (BattleStepType b in types)
            foreach (TurnReason reason in new[] { TurnReason.CrossContact, TurnReason.Miss, TurnReason.Unconditional })
            {
                bool hasContactCheck = a == S || b == S;
                if (hasContactCheck == (reason == TurnReason.Unconditional))
                    continue;
                string text = TurnNarrator.Describe(Event(a, b, reason, 0, 0));
                StringAssert.DoesNotContain("판정", text, $"{a} vs {b} ({reason})");
                StringAssert.DoesNotContain("접촉 성공", text, $"{a} vs {b} ({reason})");
                StringAssert.DoesNotContain("접촉 실패", text, $"{a} vs {b} ({reason})");
            }
        }
    }
}
