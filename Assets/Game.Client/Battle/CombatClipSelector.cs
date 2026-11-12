using System;
using System.Collections.Generic;
using Game.Sim.Battle;
using Game.Sim.Stroke;
using Game.Sim.Technique;

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
    // type and outcome come from Game.sim's TurnEvent and the points are the recorded stroke, so
    // nothing here can change a result or its timing. The battle replay and the drawing preview
    // both pick through here.
    //
    // Left/right is the attacking character's own left/right (the camera looks at both fighters
    // side-on, so screen left/right is their forward/back), and neither side is mirrored. Stroke
    // y is up for both player strokes (frame cm) and AI strokes (canvas units), so only signs are
    // compared and the two unit systems need no conversion here.
    //
    // Every vertical slash plays the downward cut, whichever way it was drawn: there is no rising
    // cut clip in use. A hidden-technique turn plays its chosen move's clip, ignoring the drawn shape.
    public static class CombatClipSelector
    {
        public const string ThrustState = "Combat_Thrust";
        public const string SlashLeftToRightState = "Combat_SlashLR";
        public const string SlashRightToLeftState = "Combat_SlashRL";
        public const string SlashVerticalState = "Combat_SlashDown";
        public const string CounterState = "Combat_Counter";

        // Review setting: true sends every slash direction but left-to-right back to the original
        // Slash (its Animator state is kept for that).
        public static bool UseLegacySlashFallback { get; set; } = false;

        // |dx| >= |dy| is horizontal, so a diagonal goes by its larger axis and an exact 45° by
        // its horizontal part; a stroke with no displacement (or fewer than 2 points) counts as
        // LeftToRight.
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

        // State for one side of a replayed turn. A counter plays Combat_Counter only when it
        // landed (Counter vs Slash with contact); a failed one keeps Guard.
        public static string StateForTurn(TurnEvent e, bool sideA, TechniqueStep? step)
        {
            BattleStepType type = sideA ? e.TypeA : e.TypeB;
            bool counterLanded = type == BattleStepType.Counter && e.Reason == TurnReason.CrossContact;
            return StateFor(type, step?.RawPoints, counterLanded, step?.HiddenMove ?? HiddenTechniqueMove.None);
        }

        public static string StateFor(BattleStepType type, IReadOnlyList<StrokePoint> points, bool counterLanded,
            HiddenTechniqueMove hiddenMove = HiddenTechniqueMove.None)
        {
            switch (hiddenMove)
            {
                case HiddenTechniqueMove.HorizontalSlash: return SlashLeftToRightState;
                case HiddenTechniqueMove.VerticalSlash: return SlashVerticalState;
                case HiddenTechniqueMove.Thrust: return ThrustState;
            }

            switch (type)
            {
                case BattleStepType.Thrust:
                    return ThrustState;
                case BattleStepType.Counter:
                    return counterLanded ? CounterState : CharacterMotionPlayer.GuardState;
                default:
                    SlashDirection direction = DirectionOf(points);
                    if (direction == SlashDirection.LeftToRight)
                        return SlashLeftToRightState;
                    if (UseLegacySlashFallback)
                        return CharacterMotionPlayer.SlashState;
                    return direction == SlashDirection.RightToLeft ? SlashRightToLeftState : SlashVerticalState;
            }
        }
    }
}
