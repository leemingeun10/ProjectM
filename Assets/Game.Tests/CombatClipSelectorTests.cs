using Game.Client.Battle;
using Game.Sim.Battle;
using Game.Sim.Stroke;
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

        [Test]
        public void StateFor_MapsTypeAndDirection()
        {
            Assert.AreEqual(CombatClipSelector.ThrustState, CombatClipSelector.StateFor(BattleStepType.Thrust, Stroke(3, 0, -3, 0)));
            Assert.AreEqual(CharacterMotionPlayer.GuardState, CombatClipSelector.StateFor(BattleStepType.Counter, Stroke(-3, 0, 3, 0)));
            Assert.AreEqual(CombatClipSelector.SlashLeftToRightState, CombatClipSelector.StateFor(BattleStepType.Slash, Stroke(-3, 0, 3, 0)));
            // Directions without their own clip yet keep the original Slash.
            Assert.AreEqual(CharacterMotionPlayer.SlashState, CombatClipSelector.StateFor(BattleStepType.Slash, Stroke(3, 0, -3, 0)));
            Assert.AreEqual(CharacterMotionPlayer.SlashState, CombatClipSelector.StateFor(BattleStepType.Slash, Stroke(0, 3, 0, -3)));
        }
    }
}
