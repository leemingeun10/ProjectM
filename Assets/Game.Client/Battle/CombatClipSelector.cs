using System;
using System.Collections.Generic;
using Game.Sim.Battle;
using Game.Sim.Stroke;

namespace Game.Client.Battle
{
    // Direction a slash was drawn in, read from its first point to its last.
    public enum SlashDirection
    {
        LeftToRight,
        RightToLeft,
        TopToBottom,
        BottomToTop,
    }

    // Picks the CharacterMotionPlayer state for one side's turn. Presentation only: the turn's
    // type comes from Game.sim's TurnEvent and the points are the recorded stroke, so nothing here
    // can change a result or its timing.
    //
    // Left/right is the attacking character's own left/right (the camera looks at both fighters
    // side-on, so screen left/right is their forward/back), and neither side is mirrored. Stroke
    // y is up for both player strokes (frame cm) and AI strokes (canvas units), so only signs are
    // compared and the two unit systems need no conversion here.
    public static class CombatClipSelector
    {
        public const string ThrustState = "Combat_Thrust";
        public const string SlashLeftToRightState = "Combat_SlashLR";

        // |dx| >= |dy| is horizontal; a stroke with no displacement (or fewer than 2 points)
        // counts as LeftToRight.
        public static SlashDirection DirectionOf(IReadOnlyList<StrokePoint> points)
        {
            if (points == null || points.Count < 2)
                return SlashDirection.LeftToRight;

            long dx = points[points.Count - 1].X.Raw - points[0].X.Raw;
            long dy = points[points.Count - 1].Y.Raw - points[0].Y.Raw;
            if (Math.Abs(dx) >= Math.Abs(dy))
                return dx < 0 ? SlashDirection.RightToLeft : SlashDirection.LeftToRight;
            return dy < 0 ? SlashDirection.TopToBottom : SlashDirection.BottomToTop;
        }

        // Counter maps to Guard regardless of whether the counter landed. Slash directions without
        // a combat clip yet fall back to the original Slash.
        public static string StateFor(BattleStepType type, IReadOnlyList<StrokePoint> points)
        {
            switch (type)
            {
                case BattleStepType.Thrust:
                    return ThrustState;
                case BattleStepType.Counter:
                    return CharacterMotionPlayer.GuardState;
                default:
                    return DirectionOf(points) == SlashDirection.LeftToRight
                        ? SlashLeftToRightState
                        : CharacterMotionPlayer.SlashState;
            }
        }
    }
}
