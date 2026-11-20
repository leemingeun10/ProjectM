using System;
using System.Collections.Generic;
using Game.Sim.Common;
using Game.Sim.Stroke;
using Game.Sim.Technique;

namespace Game.Sim.Ai
{
    // Builds a full 10-turn AI technique from a seed. Strokes are synthesized in canvas units, the
    // space battles are computed in. The player draws and saves in frame centimetres instead, and
    // a saved slot is converted to canvas units right before a battle (StrokeUnits); an AI
    // technique must never go through that conversion. Strokes are classified by the real
    // StrokeAnalyzer and added through TechniqueBuilder, like a player's. The AI never
    // sees the player's sequence: same (difficulty, seed) -> same technique, bit for bit.
    //
    // The AI draws under the player's limits: every point stays inside the player's drawing
    // frame, and a stroke's path length is a share of the player's length limit set by the
    // difficulty (AiProfile). Both come from StrokeUnits, so they follow the frame's size.
    public static class AiTechniqueGenerator
    {
        // Drawing area: the player's square frame in canvas units * Fixed.Scale, from the centre
        // to each edge.
        public const long FrameHalfRaw = StrokeUnits.FrameHalfCanvasRaw;

        // The player's stroke length limit in canvas units * Fixed.Scale.
        public const long MaxStrokeLengthRaw = StrokeUnits.MaxStrokeLengthCanvasRaw;

        // Matches StrokeInputController's resampleCount so AI strokes have the same density as
        // player strokes (contact thresholds are relative to path length, but keep parity anyway).
        public const int PointsPerStroke = 24;

        // A stroke's target length is picked this far (canvas units * Fixed.Scale) inside the
        // profile's range, so integer rounding while building it cannot push the measured length
        // out of the range or past the limit.
        private const long LengthMarginRaw = Fixed.Scale;

        // A slash is a curve of equal segments whose heading turns a little at every point; the
        // turn changes linearly from a first to a last value (tenths of a degree, either sign).
        // Same signs curl into an arc or spiral, opposite signs make an S, small ones a near line.
        // A slash longer than the frame has to curl to fit, so shapes are drawn until one both
        // fits the frame and is not a Thrust (does not touch itself).
        public const int MaxTurnTenths = 450;
        private const int SlashAttempts = 40;

        // Used when no random shape was accepted: an opening spiral that fits the frame at the
        // full length limit in every heading and never touches itself.
        public const int FallbackFirstTurnTenths = 120;
        public const int FallbackLastTurnTenths = 300;
        private const int FallbackAttempts = 8;

        // A thrust loop is an ellipse; its short radius is at least this percent of the long one.
        private const int MinLoopAspectPercent = 70;
        // Radius the unit loop is measured at before scaling it to the target path length.
        private const long LoopUnitRadiusRaw = 1000000;

        // cos(k * 15deg) * Fixed.Scale for k = 0..23. A table instead of trig keeps this integer-only.
        private static readonly int[] Cos24 =
        {
            1000, 966, 866, 707, 500, 259, 0, -259, -500, -707, -866, -966,
            -1000, -966, -866, -707, -500, -259, 0, 259, 500, 707, 866, 966
        };

        private static int Sin24(int k) => Cos24[(k + 18) % 24];

        public static List<TechniqueStep> Generate(AiDifficulty difficulty, ulong seed)
            => Generate(AiProfile.For(difficulty), seed);

        public static List<TechniqueStep> Generate(AiProfile profile, ulong seed)
        {
            if (profile.CounterCount < 0 || profile.CounterCount > TechniqueBuilder.MaxCounters)
                throw new ArgumentOutOfRangeException(nameof(profile), $"CounterCount must be 0..{TechniqueBuilder.MaxCounters}.");
            if (profile.LengthMinPercent <= 0 || profile.LengthMaxPercent > 100 || MinLengthRaw(profile) > MaxLengthRaw(profile))
                throw new ArgumentOutOfRangeException(nameof(profile), "Length percents must satisfy 0 < min < max <= 100.");

            var rng = new DeterministicRng(seed);
            bool[] counterTurns = PickCounterTurns(rng, profile.CounterCount);

            var builder = new TechniqueBuilder();
            for (int turn = 0; turn < TechniqueBuilder.MaxTurns; turn++)
            {
                bool isCounter = counterTurns[turn];
                bool isThrust = !isCounter && rng.Chance(profile.ThrustPercent);

                StrokePoint[] points = isThrust ? MakeThrustLoop(rng, profile) : MakeSlash(rng, profile);
                StrokeResult stroke = StrokeAnalyzer.Analyze(points);

                if (stroke.Type != (isThrust ? StrokeType.Thrust : StrokeType.Slash))
                    throw new InvalidOperationException($"Generated {(isThrust ? "thrust loop" : "slash")} was classified as {stroke.Type}.");

                TechniqueAddResult result = builder.TryAddStep(isCounter, stroke, points);
                if (result != TechniqueAddResult.Success)
                    throw new InvalidOperationException($"AI step {turn} rejected: {result}.");
            }

            return new List<TechniqueStep>(builder.Steps);
        }

        // Partial Fisher-Yates over the turn indices: the first `count` picks become counters.
        private static bool[] PickCounterTurns(DeterministicRng rng, int count)
        {
            int[] order = new int[TechniqueBuilder.MaxTurns];
            for (int i = 0; i < order.Length; i++)
                order[i] = i;

            var isCounter = new bool[TechniqueBuilder.MaxTurns];
            for (int i = 0; i < count; i++)
            {
                int j = rng.Range(i, order.Length);
                (order[i], order[j]) = (order[j], order[i]);
                isCounter[order[i]] = true;
            }
            return isCounter;
        }

        // Smallest and largest target length a profile's strokes are picked from, whole canvas
        // units * Fixed.Scale, LengthMarginRaw inside the profile's share of the limit.
        private static long MinLengthRaw(AiProfile profile)
            => (MaxStrokeLengthRaw * profile.LengthMinPercent / 100 + LengthMarginRaw + Fixed.Scale - 1) / Fixed.Scale * Fixed.Scale;

        private static long MaxLengthRaw(AiProfile profile)
            => (MaxStrokeLengthRaw * profile.LengthMaxPercent / 100 - LengthMarginRaw) / Fixed.Scale * Fixed.Scale;

        private static long PickLengthRaw(DeterministicRng rng, AiProfile profile)
        {
            int min = (int)(MinLengthRaw(profile) / Fixed.Scale);
            int max = (int)(MaxLengthRaw(profile) / Fixed.Scale);
            return rng.Range(min, max + 1) * Fixed.Scale;
        }

        private static StrokePoint[] MakeSlash(DeterministicRng rng, AiProfile profile)
        {
            long length = PickLengthRaw(rng, profile);

            for (int attempt = 0; attempt < SlashAttempts; attempt++)
            {
                int heading = rng.Range(0, 360);
                int firstTurn = rng.Range(-MaxTurnTenths, MaxTurnTenths + 1);
                int lastTurn = rng.Range(-MaxTurnTenths, MaxTurnTenths + 1);
                if (TryPlaceCurve(rng, BuildCurve(length, heading, firstTurn, lastTurn), out StrokePoint[] placed))
                    return placed;
            }

            int sign = rng.Chance(50) ? 1 : -1;
            for (int attempt = 0; attempt < FallbackAttempts; attempt++)
            {
                StrokePoint[] spiral = BuildCurve(length, rng.Range(0, 360), sign * FallbackFirstTurnTenths, sign * FallbackLastTurnTenths);
                if (TryPlaceCurve(rng, spiral, out StrokePoint[] placed))
                    return placed;
            }
            throw new InvalidOperationException("The fallback slash did not fit the drawing frame.");
        }

        // The curve MakeSlash draws, starting at the origin: PointsPerStroke - 1 equal segments
        // whose heading starts at headingDegrees and turns at every interior point, by
        // firstTurnTenths at the first one through to lastTurnTenths at the last (linear in
        // between). Pure integer math; lengthRaw is canvas units * Fixed.Scale.
        public static StrokePoint[] BuildCurve(long lengthRaw, int headingDegrees, int firstTurnTenths, int lastTurnTenths)
        {
            int segments = PointsPerStroke - 1;
            long step = lengthRaw / segments;
            var points = new StrokePoint[PointsPerStroke];
            long x = 0, y = 0;
            int headingTenths = headingDegrees * 10;
            points[0] = new StrokePoint(x, y);
            for (int k = 0; k < segments; k++)
            {
                if (k > 0)
                    headingTenths += firstTurnTenths + (lastTurnTenths - firstTurnTenths) * (k - 1) / (segments - 2);
                int degrees = RoundTenthsToDegrees(headingTenths);
                x += step * FixedTrig.Cos(degrees) / FixedTrig.Scale;
                y += step * FixedTrig.Sin(degrees) / FixedTrig.Scale;
                points[k + 1] = new StrokePoint(x, y);
            }
            return points;
        }

        // Nearest whole degree, halves up, for negative headings too (floor division).
        private static int RoundTenthsToDegrees(int tenths)
        {
            int shifted = tenths + 5;
            return shifted >= 0 ? shifted / 10 : -((-shifted + 9) / 10);
        }

        // Accepts a curve that is a Slash and fits the frame, moving it to a random place where
        // all of it is inside. Draws from the rng only when the curve is accepted.
        private static bool TryPlaceCurve(DeterministicRng rng, StrokePoint[] curve, out StrokePoint[] placed)
        {
            placed = null;
            long minX = long.MaxValue, maxX = long.MinValue, minY = long.MaxValue, maxY = long.MinValue;
            foreach (StrokePoint p in curve)
            {
                minX = Math.Min(minX, p.X.Raw);
                maxX = Math.Max(maxX, p.X.Raw);
                minY = Math.Min(minY, p.Y.Raw);
                maxY = Math.Max(maxY, p.Y.Raw);
            }
            if (maxX - minX > 2 * FrameHalfRaw || maxY - minY > 2 * FrameHalfRaw)
                return false;
            // Self-contact does not depend on where the curve sits, so it is judged before placing.
            if (StrokeAnalyzer.Analyze(curve).Type != StrokeType.Slash)
                return false;

            long offsetX = rng.Range((int)(-FrameHalfRaw - minX), (int)(FrameHalfRaw - maxX) + 1);
            long offsetY = rng.Range((int)(-FrameHalfRaw - minY), (int)(FrameHalfRaw - maxY) + 1);
            placed = new StrokePoint[curve.Length];
            for (int i = 0; i < curve.Length; i++)
                placed[i] = new StrokePoint(curve[i].X.Raw + offsetX, curve[i].Y.Raw + offsetY);
            return true;
        }

        // Closed ellipse: 24 vertices plus the start point repeated, like a player closing a loop.
        // Its path length (the 24-gon's perimeter) is the picked length; a loop too elongated to
        // fit the frame at that length is drawn as a circle instead, which always fits.
        private static StrokePoint[] MakeThrustLoop(DeterministicRng rng, AiProfile profile)
        {
            long length = PickLengthRaw(rng, profile);
            int aspect = rng.Range(MinLoopAspectPercent, 101);
            bool wide = rng.Chance(50);

            long shortUnit = LoopUnitRadiusRaw * aspect / 100;
            ScaleLoop(length, wide ? LoopUnitRadiusRaw : shortUnit, wide ? shortUnit : LoopUnitRadiusRaw, out long rx, out long ry);
            if (rx > FrameHalfRaw || ry > FrameHalfRaw)
                ScaleLoop(length, LoopUnitRadiusRaw, LoopUnitRadiusRaw, out rx, out ry);
            if (rx > FrameHalfRaw || ry > FrameHalfRaw)
                throw new InvalidOperationException("A thrust loop at the length limit does not fit the drawing frame.");

            long cx = rng.Range((int)-(FrameHalfRaw - rx), (int)(FrameHalfRaw - rx) + 1);
            long cy = rng.Range((int)-(FrameHalfRaw - ry), (int)(FrameHalfRaw - ry) + 1);

            var points = new StrokePoint[PointsPerStroke + 1];
            for (int k = 0; k < PointsPerStroke; k++)
                points[k] = new StrokePoint(cx + Cos24[k] * rx / Fixed.Scale, cy + Sin24(k) * ry / Fixed.Scale);
            points[PointsPerStroke] = points[0];
            return points;
        }

        // Radii (canvas units * Fixed.Scale) of the 24-gon with the given proportions whose
        // perimeter is lengthRaw.
        private static void ScaleLoop(long lengthRaw, long unitRx, long unitRy, out long rx, out long ry)
        {
            long perimeter = 0;
            for (int k = 0; k < PointsPerStroke; k++)
            {
                int next = (k + 1) % PointsPerStroke;
                long dx = (Cos24[next] - Cos24[k]) * unitRx / Fixed.Scale;
                long dy = (Sin24(next) - Sin24(k)) * unitRy / Fixed.Scale;
                perimeter += IntegerMath.IntegerSqrt(dx * dx + dy * dy);
            }
            rx = unitRx * lengthRaw / perimeter;
            ry = unitRy * lengthRaw / perimeter;
        }
    }
}
