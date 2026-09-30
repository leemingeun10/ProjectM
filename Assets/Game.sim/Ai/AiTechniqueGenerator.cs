using System;
using System.Collections.Generic;
using Game.Sim.Common;
using Game.Sim.Stroke;
using Game.Sim.Technique;

namespace Game.Sim.Ai
{
    // Builds a full 10-turn AI technique from a seed. Strokes are synthesized in the same canvas
    // coordinate space the player draws in and classified by the real StrokeAnalyzer, then added
    // through TechniqueBuilder, so the AI obeys exactly the rules a player does. The AI never
    // sees the player's sequence: same (difficulty, seed) -> same technique, bit for bit.
    public static class AiTechniqueGenerator
    {
        // Drawing area the AI keeps its strokes inside (whole canvas units, origin at centre).
        // Slightly inside the 1080x1920 reference canvas so strokes stay on screen.
        public const int HalfWidth = 480;
        public const int HalfHeight = 880;

        // Matches StrokeInputController's resampleCount so AI strokes have the same density as
        // player strokes (contact thresholds are relative to path length, but keep parity anyway).
        public const int PointsPerStroke = 24;

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

            var rng = new DeterministicRng(seed);
            bool[] counterTurns = PickCounterTurns(rng, profile.CounterCount);

            var builder = new TechniqueBuilder();
            for (int turn = 0; turn < TechniqueBuilder.MaxTurns; turn++)
            {
                bool isCounter = counterTurns[turn];
                bool isThrust = !isCounter && rng.Chance(profile.ThrustPercent);

                StrokePoint[] points = isThrust ? MakeThrustLoop(rng, profile) : MakeSlash(rng, profile);
                StrokeResult stroke = StrokeAnalyzer.Analyze(points);

                if (isThrust && stroke.Type != StrokeType.Thrust)
                    throw new InvalidOperationException("Generated thrust loop was not classified as Thrust.");

                // A curved slash that happens to trip the self-contact check falls back to a
                // straight line, which never self-contacts at this point density.
                if (!isThrust && stroke.Type != StrokeType.Slash)
                {
                    points = MakeStraightFallback(points);
                    stroke = StrokeAnalyzer.Analyze(points);
                }

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

        // Quadratic Bezier from P0 to P2, bent by a control point offset perpendicular to the chord.
        // All coordinates are Fixed raw values; Cos24 entries are already scaled by Fixed.Scale.
        private static StrokePoint[] MakeSlash(DeterministicRng rng, AiProfile profile)
        {
            int length = rng.Range(profile.SlashLengthMin, profile.SlashLengthMax);
            int dir = rng.Range(0, 24);
            int bend = rng.Range(-length / 4, length / 4 + 1);

            long dx = Cos24[dir];
            long dy = Sin24(dir);
            int absBend = Math.Abs(bend);
            int extentX = (int)(Math.Abs(dx) * length / (2 * Fixed.Scale)) + absBend;
            int extentY = (int)(Math.Abs(dy) * length / (2 * Fixed.Scale)) + absBend;

            long cx = rng.Range(-(HalfWidth - extentX), HalfWidth - extentX + 1) * Fixed.Scale;
            long cy = rng.Range(-(HalfHeight - extentY), HalfHeight - extentY + 1) * Fixed.Scale;

            long p0x = cx - dx * length / 2, p0y = cy - dy * length / 2;
            long p2x = cx + dx * length / 2, p2y = cy + dy * length / 2;
            long p1x = cx - dy * bend, p1y = cy + dx * bend;

            int n = PointsPerStroke - 1;
            long n2 = (long)n * n;
            var points = new StrokePoint[PointsPerStroke];
            for (int i = 0; i <= n; i++)
            {
                long a = (long)(n - i) * (n - i);
                long b = 2L * (n - i) * i;
                long c = (long)i * i;
                points[i] = new StrokePoint((a * p0x + b * p1x + c * p2x) / n2, (a * p0y + b * p1y + c * p2y) / n2);
            }
            return points;
        }

        private static StrokePoint[] MakeStraightFallback(StrokePoint[] curved)
        {
            StrokePoint start = curved[0];
            StrokePoint end = curved[curved.Length - 1];
            int n = curved.Length - 1;
            var points = new StrokePoint[curved.Length];
            for (int i = 0; i <= n; i++)
            {
                points[i] = new StrokePoint(
                    start.X.Raw + (end.X.Raw - start.X.Raw) * i / n,
                    start.Y.Raw + (end.Y.Raw - start.Y.Raw) * i / n);
            }
            return points;
        }

        // Closed ellipse: 24 vertices plus the start point repeated, like a player closing a loop.
        private static StrokePoint[] MakeThrustLoop(DeterministicRng rng, AiProfile profile)
        {
            int rx = rng.Range(profile.ThrustRadiusMin, profile.ThrustRadiusMax);
            int ry = rng.Range(profile.ThrustRadiusMin, profile.ThrustRadiusMax);
            long cx = rng.Range(-(HalfWidth - rx), HalfWidth - rx + 1) * Fixed.Scale;
            long cy = rng.Range(-(HalfHeight - ry), HalfHeight - ry + 1) * Fixed.Scale;

            var points = new StrokePoint[PointsPerStroke + 1];
            for (int k = 0; k < PointsPerStroke; k++)
                points[k] = new StrokePoint(cx + (long)Cos24[k] * rx, cy + (long)Sin24(k) * ry);
            points[PointsPerStroke] = points[0];
            return points;
        }
    }
}
