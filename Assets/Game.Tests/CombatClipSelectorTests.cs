using Game.Client.Battle;
using Game.Sim.Battle;
using Game.Sim.Stroke;
using Game.Sim.Technique;
using NUnit.Framework;

namespace Game.Tests
{
    public class CombatClipSelectorTests
    {
        // Whole units, y up; only the first and last points matter.
        private static StrokePoint[] Stroke(long x0, long y0, long x1, long y1)
            => new[] { StrokePoint.FromInt(x0, y0), StrokePoint.FromInt((x0 + x1) / 2 + 7, (y0 + y1) / 2 - 7), StrokePoint.FromInt(x1, y1) };

        [Test]
        public void Horizontal_SignOfDxPicksSide()
        {
            Assert.AreEqual(SlashDirection.LeftToRight, CombatClipSelector.DirectionOf(Stroke(-3, 0, 3, 1)));
            Assert.AreEqual(SlashDirection.RightToLeft, CombatClipSelector.DirectionOf(Stroke(3, 0, -3, -1)));
        }

        [Test]
        public void Vertical_YIsUp()
        {
            Assert.AreEqual(SlashDirection.TopToBottom, CombatClipSelector.DirectionOf(Stroke(0, 3, 1, -3)));
            Assert.AreEqual(SlashDirection.BottomToTop, CombatClipSelector.DirectionOf(Stroke(0, -3, -1, 3)));
        }

        [Test]
        public void Diagonal_TieCountsAsHorizontal()
        {
            Assert.AreEqual(SlashDirection.LeftToRight, CombatClipSelector.DirectionOf(Stroke(0, 0, 2, -2)));
            Assert.AreEqual(SlashDirection.RightToLeft, CombatClipSelector.DirectionOf(Stroke(0, 0, -2, 2)));
        }

        [Test]
        public void UsesNetDisplacement_NotTheMiddle()
        {
            // Goes far up and comes back: net movement is left to right.
            var points = new[] { StrokePoint.FromInt(0, 0), StrokePoint.FromInt(1, 50), StrokePoint.FromInt(5, 1) };
            Assert.AreEqual(SlashDirection.LeftToRight, CombatClipSelector.DirectionOf(points));
        }

        [Test]
        public void Degenerate_DefaultsToLeftToRight()
        {
            Assert.AreEqual(SlashDirection.LeftToRight, CombatClipSelector.DirectionOf(null));
            Assert.AreEqual(SlashDirection.LeftToRight, CombatClipSelector.DirectionOf(new[] { StrokePoint.FromInt(1, 1) }));
            Assert.AreEqual(SlashDirection.LeftToRight, CombatClipSelector.DirectionOf(Stroke(2, 2, 2, 2)));
        }

        private bool savedFallback;

        [SetUp]
        public void SaveFallback() => savedFallback = CombatClipSelector.UseLegacySlashFallback;

        [TearDown]
        public void RestoreFallback() => CombatClipSelector.UseLegacySlashFallback = savedFallback;

        private static TechniqueStep Step(StrokePoint[] stroke, string hiddenId = null, HiddenTechniqueMove move = HiddenTechniqueMove.None)
            => new TechniqueStep(0, false, StrokeAnalyzer.Analyze(stroke), stroke, hiddenId, move);

        private static string Slash(StrokePoint[] stroke) => CombatClipSelector.StateFor(BattleStepType.Slash, stroke, false);

        private static readonly StrokePoint[] RightToLeft = Stroke(3, 0, -3, 0);
        private static readonly StrokePoint[] TopToBottom = Stroke(0, 3, 0, -3);
        private static readonly StrokePoint[] BottomToTop = Stroke(0, -3, 0, 3);

        // Every test restores the flag in TearDown, so this sees the value the game starts with.
        [Test]
        public void FallbackIsOffByDefault()
            => Assert.IsFalse(CombatClipSelector.UseLegacySlashFallback);

        [Test]
        public void StateFor_EachSlashDirectionHasItsClip_VerticalIsAlwaysTheDownwardCut()
        {
            CombatClipSelector.UseLegacySlashFallback = false;
            Assert.AreEqual(CombatClipSelector.SlashLeftToRightState, Slash(Stroke(-3, 0, 3, 0)));
            Assert.AreEqual(CombatClipSelector.SlashRightToLeftState, Slash(RightToLeft));
            Assert.AreEqual("Combat_SlashDown", Slash(TopToBottom));
            // Drawn upwards, it still plays the downward cut: no rising clip is used.
            Assert.AreEqual("Combat_SlashDown", Slash(BottomToTop));
        }

        [Test]
        public void StateFor_DiagonalGoesByLargerAxis()
        {
            CombatClipSelector.UseLegacySlashFallback = false;
            Assert.AreEqual(CombatClipSelector.SlashRightToLeftState, Slash(Stroke(4, 1, -4, -2)));   // mostly leftward
            Assert.AreEqual(CombatClipSelector.SlashVerticalState, Slash(Stroke(1, 4, -2, -4)));   // mostly downward
            Assert.AreEqual(CombatClipSelector.SlashVerticalState, Slash(Stroke(-1, -4, 2, 4)));   // mostly upward
            Assert.AreEqual(CombatClipSelector.SlashLeftToRightState, Slash(Stroke(0, 0, 3, -3)));    // exact 45° is horizontal
            Assert.AreEqual(CombatClipSelector.SlashLeftToRightState, Slash(Stroke(2, 2, 2, 2)));     // no displacement
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StateFor_ThrustCounterAndLeftToRight_IgnoreFallback(bool fallback)
        {
            CombatClipSelector.UseLegacySlashFallback = fallback;
            Assert.AreEqual(CombatClipSelector.ThrustState, CombatClipSelector.StateFor(BattleStepType.Thrust, RightToLeft, false));
            Assert.AreEqual(CharacterMotionPlayer.GuardState, CombatClipSelector.StateFor(BattleStepType.Counter, RightToLeft, false));
            Assert.AreEqual(CombatClipSelector.CounterState, CombatClipSelector.StateFor(BattleStepType.Counter, RightToLeft, true));
            Assert.AreEqual(CombatClipSelector.SlashLeftToRightState, Slash(Stroke(-3, 0, 3, 0)));
        }

        [Test]
        public void StateFor_FallbackOn_OtherDirectionsUseLegacySlash()
        {
            CombatClipSelector.UseLegacySlashFallback = true;
            foreach (StrokePoint[] stroke in new[] { RightToLeft, TopToBottom, BottomToTop })
                Assert.AreEqual(CharacterMotionPlayer.SlashState, Slash(stroke));
        }

        [Test]
        public void StateForTurn_CounterClipOnlyWhenTheCounterLanded()
        {
            var landed = new TurnEvent(0, BattleStepType.Counter, BattleStepType.Slash, 0, 20, TurnReason.CrossContact, 100, 80);
            var missed = new TurnEvent(0, BattleStepType.Counter, BattleStepType.Slash, 10, 0, TurnReason.Miss, 90, 100);
            var vsThrust = new TurnEvent(0, BattleStepType.Thrust, BattleStepType.Counter, 0, 6, TurnReason.Unconditional, 100, 94);
            var bothCounter = new TurnEvent(0, BattleStepType.Counter, BattleStepType.Counter, 0, 0, TurnReason.Unconditional, 100, 100);

            Assert.AreEqual(CombatClipSelector.CounterState, CombatClipSelector.StateForTurn(landed, true, Step(RightToLeft)));
            Assert.AreEqual(CombatClipSelector.SlashRightToLeftState, CombatClipSelector.StateForTurn(landed, false, Step(RightToLeft)));
            Assert.AreEqual(CharacterMotionPlayer.GuardState, CombatClipSelector.StateForTurn(missed, true, Step(RightToLeft)));
            Assert.AreEqual(CharacterMotionPlayer.GuardState, CombatClipSelector.StateForTurn(vsThrust, false, Step(RightToLeft)));
            Assert.AreEqual(CombatClipSelector.ThrustState, CombatClipSelector.StateForTurn(vsThrust, true, Step(RightToLeft)));
            Assert.AreEqual(CharacterMotionPlayer.GuardState, CombatClipSelector.StateForTurn(bothCounter, true, Step(RightToLeft)));
            Assert.AreEqual(CharacterMotionPlayer.GuardState, CombatClipSelector.StateForTurn(bothCounter, false, Step(RightToLeft)));
        }

        // 삼재: the chosen move picks the clip, whatever the drawn shape or its direction.
        [TestCase(HiddenTechniqueMove.HorizontalSlash, "Combat_SlashLR")]
        [TestCase(HiddenTechniqueMove.VerticalSlash, "Combat_SlashDown")]
        [TestCase(HiddenTechniqueMove.Thrust, "Combat_Thrust")]
        public void HiddenMove_PicksItsClip(HiddenTechniqueMove move, string state)
        {
            foreach (bool fallback in new[] { false, true })
            {
                CombatClipSelector.UseLegacySlashFallback = fallback;
                BattleStepType type = move == HiddenTechniqueMove.Thrust ? BattleStepType.Thrust : BattleStepType.Slash;
                foreach (StrokePoint[] stroke in new[] { RightToLeft, TopToBottom, BottomToTop, Triangle })
                {
                    Assert.AreEqual(state, CombatClipSelector.StateFor(type, stroke, false, move));
                    var e = new TurnEvent(0, type, BattleStepType.Counter, 0, 10, TurnReason.Unconditional, 100, 90);
                    Assert.AreEqual(state, CombatClipSelector.StateForTurn(e, true, Step(stroke, "triangle", move)));
                }
            }
        }

        private static readonly StrokePoint[] Triangle =
            { StrokePoint.FromInt(0, 0), StrokePoint.FromInt(400, 0), StrokePoint.FromInt(0, 360), StrokePoint.FromInt(0, 0) };

        [Test]
        public void StateForTurn_WithoutAStep_UsesTheTurnTypeAlone()
        {
            var e = new TurnEvent(0, BattleStepType.Slash, BattleStepType.Thrust, 0, 10, TurnReason.Miss, 100, 90);
            Assert.AreEqual(CombatClipSelector.SlashLeftToRightState, CombatClipSelector.StateForTurn(e, true, null));
            Assert.AreEqual(CombatClipSelector.ThrustState, CombatClipSelector.StateForTurn(e, false, null));
        }
    }
}
