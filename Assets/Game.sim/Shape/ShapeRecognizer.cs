using System;
using System.Collections.Generic;
using Game.Sim.Common;

namespace Game.Sim.Shape
{
    // $1 unistroke recognizer in integer arithmetic: resample the drawn points to a fixed count
    // spaced evenly along the path, move the centroid to the origin and scale to a fixed size, then
    // compare point-by-point with each template at every RotationStepDegrees rotation.
    // Everything is integer +, *, truncating / and IntegerMath.IntegerSqrt, so the same drawing gets
    // the same score on every device.
    //
    // Inputs are expected within about ±1,000,000 per axis (the client sends centimetres * 1000);
    // far larger values can overflow the long products.
    public static class ShapeRecognizer
    {
        public const int PointCount = 64;

        // Normalize scales so the root-mean-square distance from the centroid is this. RMS radius,
        // unlike a bounding box, does not change when the shape rotates, so the rotation search
        // compares like with like.
        public const int NormalizedRadius = 500;

        // Average point distance (in normalized units) that scores 0; distance 0 scores MaxScore.
        // 1000 is twice NormalizedRadius. Tuned so a roughly drawn triangle stays above the
        // default threshold while a circle or square falls below both templates.
        public const int ZeroScoreDistance = 1000;

        public const int MaxScore = 10000;
        public const int DefaultThreshold = 9000;
        public const int RotationStepDegrees = 10;

        // A stroke whose ends are at most this far apart (in normalized units, 40% of
        // NormalizedRadius) counts as a closed loop, and Recognize then tries every starting point
        // along it: a flower started between petals or a triangle started mid-edge is the same shape.
        public const int ClosedGapMax = 200;

        // Returns n points evenly spaced along the path through rawPoints, first and last included.
        // A path of zero length (all points equal) returns n copies of the first point.
        public static ShapePoint[] Resample(IReadOnlyList<ShapePoint> rawPoints, int n)
        {
            if (rawPoints == null)
                throw new ArgumentNullException(nameof(rawPoints));
            if (rawPoints.Count == 0)
                throw new ArgumentException("At least one point is required.", nameof(rawPoints));
            if (n < 2)
                throw new ArgumentOutOfRangeException(nameof(n), n, "At least 2 points are required.");

            var cumulative = new long[rawPoints.Count];
            for (int i = 1; i < rawPoints.Count; i++)
                cumulative[i] = cumulative[i - 1] + Distance(rawPoints[i - 1], rawPoints[i]);
            long total = cumulative[rawPoints.Count - 1];

            var result = new ShapePoint[n];
            if (total == 0)
            {
                for (int i = 0; i < n; i++)
                    result[i] = rawPoints[0];
                return result;
            }

            int segment = 0;
            for (int i = 0; i < n; i++)
            {
                long target = total * i / (n - 1);
                while (segment < rawPoints.Count - 2 && cumulative[segment + 1] < target)
                    segment++;

                ShapePoint a = rawPoints[segment];
                ShapePoint b = rawPoints[segment + 1];
                long segmentLength = cumulative[segment + 1] - cumulative[segment];
                if (segmentLength == 0)
                {
                    result[i] = a;
                    continue;
                }

                long along = target - cumulative[segment];
                result[i] = new ShapePoint(
                    (int)(a.X + (long)(b.X - a.X) * along / segmentLength),
                    (int)(a.Y + (long)(b.Y - a.Y) * along / segmentLength));
            }
            return result;
        }

        // Returns a copy moved so the centroid is the origin and scaled so the RMS distance from it
        // is NormalizedRadius, making the result independent of where and how big it was drawn.
        // Points that all coincide come back as all-zero (there is no size to scale).
        public static ShapePoint[] Normalize(ShapePoint[] points)
        {
            if (points == null)
                throw new ArgumentNullException(nameof(points));
            if (points.Length == 0)
                return new ShapePoint[0];

            long sumX = 0, sumY = 0;
            foreach (ShapePoint p in points)
            {
                sumX += p.X;
                sumY += p.Y;
            }
            long cx = sumX / points.Length;
            long cy = sumY / points.Length;

            long sumSquares = 0;
            foreach (ShapePoint p in points)
            {
                long dx = p.X - cx;
                long dy = p.Y - cy;
                sumSquares += dx * dx + dy * dy;
            }
            long rms = IntegerMath.IntegerSqrt(sumSquares / points.Length);

            var result = new ShapePoint[points.Length];
            if (rms == 0)
                return result;

            for (int i = 0; i < points.Length; i++)
            {
                result[i] = new ShapePoint(
                    (int)((points[i].X - cx) * NormalizedRadius / rms),
                    (int)((points[i].Y - cy) * NormalizedRadius / rms));
            }
            return result;
        }

        // Rotates the candidate about the origin by 0, 10, ..., 350 degrees and scores the rotation
        // whose average point-to-point distance to the template is smallest:
        // MaxScore * (1 - distance / ZeroScoreDistance), floored at 0.
        // Both arrays must be normalized and the same length.
        public static int BestRotationScore(ShapePoint[] normalizedCandidate, ShapePoint[] normalizedTemplate)
        {
            if (normalizedCandidate == null)
                throw new ArgumentNullException(nameof(normalizedCandidate));
            if (normalizedTemplate == null)
                throw new ArgumentNullException(nameof(normalizedTemplate));
            if (normalizedCandidate.Length != normalizedTemplate.Length || normalizedCandidate.Length == 0)
                throw new ArgumentException("Candidate and template must have the same, non-zero number of points.");

            long best = long.MaxValue;
            for (int degrees = 0; degrees < 360; degrees += RotationStepDegrees)
            {
                long distance = AverageDistance(normalizedCandidate, normalizedTemplate, degrees);
                if (distance < best)
                    best = distance;
            }

            long score = MaxScore - best * MaxScore / ZeroScoreDistance;
            return score < 0 ? 0 : (int)score;
        }

        // Best-scoring template at or above thresholdScore, or null when none reaches it (also for
        // fewer than 2 points or a stroke with no length). A shape drawn the other way round
        // (clockwise instead of anticlockwise) counts too: the reversed point order is scored as well.
        // A closed stroke (ends within ClosedGapMax) is also scored from every starting point.
        // On a tied score the template listed first wins.
        public static ShapeMatchResult? Recognize(IReadOnlyList<ShapePoint> rawPoints, IReadOnlyList<ShapeTemplate> templates, int thresholdScore)
        {
            if (templates == null)
                throw new ArgumentNullException(nameof(templates));
            if (rawPoints == null || rawPoints.Count < 2)
                return null;

            ShapePoint[] candidate = Normalize(Resample(rawPoints, PointCount));
            if (IsAllZero(candidate))
                return null;

            List<ShapePoint[]> orderings = Orderings(candidate);

            ShapeMatchResult? best = null;
            foreach (ShapeTemplate template in templates)
            {
                int score = 0;
                foreach (ShapePoint[] ordering in orderings)
                    score = Math.Max(score, BestRotationScore(ordering, template.NormalizedPoints));
                if (score >= thresholdScore && (best == null || score > best.Value.Score))
                    best = new ShapeMatchResult(template.Id, score);
            }
            return best;
        }

        // The point orders Recognize compares: the stroke forwards and reversed, and for a closed
        // stroke the same from every starting point. The last resampled point of a closed stroke
        // sits on the first, so the loop is the first n - 1 points, closed again after the shift.
        private static List<ShapePoint[]> Orderings(ShapePoint[] candidate)
        {
            int n = candidate.Length;
            var forwards = new List<ShapePoint[]> { candidate };
            if (Distance(candidate[0], candidate[n - 1]) <= ClosedGapMax)
            {
                for (int start = 1; start < n - 1; start++)
                {
                    var shifted = new ShapePoint[n];
                    for (int i = 0; i < n - 1; i++)
                        shifted[i] = candidate[(start + i) % (n - 1)];
                    shifted[n - 1] = shifted[0];
                    forwards.Add(shifted);
                }
            }

            var orderings = new List<ShapePoint[]>(forwards.Count * 2);
            foreach (ShapePoint[] ordering in forwards)
            {
                var reversed = new ShapePoint[n];
                for (int i = 0; i < n; i++)
                    reversed[i] = ordering[n - 1 - i];
                orderings.Add(ordering);
                orderings.Add(reversed);
            }
            return orderings;
        }

        private static long AverageDistance(ShapePoint[] candidate, ShapePoint[] template, int degrees)
        {
            long cos = FixedTrig.Cos(degrees);
            long sin = FixedTrig.Sin(degrees);
            long sum = 0;
            for (int i = 0; i < candidate.Length; i++)
            {
                long x = candidate[i].X;
                long y = candidate[i].Y;
                long rx = (x * cos - y * sin) / FixedTrig.Scale;
                long ry = (x * sin + y * cos) / FixedTrig.Scale;
                long dx = rx - template[i].X;
                long dy = ry - template[i].Y;
                sum += IntegerMath.IntegerSqrt(dx * dx + dy * dy);
            }
            return sum / candidate.Length;
        }

        private static long Distance(ShapePoint a, ShapePoint b)
        {
            long dx = b.X - (long)a.X;
            long dy = b.Y - (long)a.Y;
            return IntegerMath.IntegerSqrt(dx * dx + dy * dy);
        }

        private static bool IsAllZero(ShapePoint[] points)
        {
            foreach (ShapePoint p in points)
            {
                if (p.X != 0 || p.Y != 0)
                    return false;
            }
            return true;
        }
    }
}
