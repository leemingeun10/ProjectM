using System;
using System.Collections.Generic;

namespace Game.Sim.Stroke
{
    public static class StrokeAnalyzer
    {
        // Self-contact distance = this ratio * total path length. Segments that come within
        // that distance of each other (or cross) mark the stroke as a Thrust. Tune by playtesting.
        public static readonly Fixed SelfContactThreshold = Fixed.FromRatio(3, 100);

        // Cross-contact distance between two different strokes = this ratio * their combined
        // path length. Starts equal to SelfContactThreshold; tune separately by playtesting.
        public static readonly Fixed CrossContactThreshold = Fixed.FromRatio(3, 100);

        public static StrokeResult Analyze(IReadOnlyList<StrokePoint> points)
        {
            if (points == null || points.Count < 2)
                return StrokeResult.Slash(Fixed.Zero, Fixed.Zero, Fixed.Zero, Fixed.Zero, Fixed.Zero);

            int segmentCount = points.Count - 1;

            Fixed totalLength = PathLength(points);

            Fixed threshold = totalLength * SelfContactThreshold;

            bool selfContact = false;
            for (int i = 0; i < segmentCount && !selfContact; i++)
            {
                for (int j = i + 2; j < segmentCount; j++)
                {
                    Fixed d = SegmentToSegmentDistance(points[i], points[i + 1], points[j], points[j + 1]);
                    if (d.Raw <= threshold.Raw)
                    {
                        selfContact = true;
                        break;
                    }
                }
            }

            if (selfContact)
                return StrokeResult.Thrust(ShoelaceArea(points));

            (Fixed sx, Fixed sy) = Direction(points[0], points[1]);
            (Fixed ex, Fixed ey) = Direction(points[points.Count - 2], points[points.Count - 1]);
            return StrokeResult.Slash(totalLength, sx, sy, ex, ey);
        }

        // True if any segment of stroke a crosses or comes within the cross-contact distance of
        // any segment of stroke b. A stroke with fewer than 2 points has no segments and never
        // makes contact.
        public static bool CrossStrokeContact(IReadOnlyList<StrokePoint> a, IReadOnlyList<StrokePoint> b)
        {
            if (a == null || b == null || a.Count < 2 || b.Count < 2)
                return false;

            Fixed threshold = (PathLength(a) + PathLength(b)) * CrossContactThreshold;

            for (int i = 0; i < a.Count - 1; i++)
            {
                for (int j = 0; j < b.Count - 1; j++)
                {
                    Fixed d = SegmentToSegmentDistance(a[i], a[i + 1], b[j], b[j + 1]);
                    if (d.Raw <= threshold.Raw)
                        return true;
                }
            }

            return false;
        }

        private static Fixed PathLength(IReadOnlyList<StrokePoint> points)
        {
            Fixed length = Fixed.Zero;
            for (int i = 0; i < points.Count - 1; i++)
                length += Distance(points[i], points[i + 1]);
            return length;
        }

        private static (Fixed, Fixed) Direction(StrokePoint from, StrokePoint to)
        {
            Fixed dx = to.X - from.X;
            Fixed dy = to.Y - from.Y;
            Fixed len = Fixed.Sqrt(dx * dx + dy * dy);
            if (len.Raw == 0) return (Fixed.Zero, Fixed.Zero);
            return (dx / len, dy / len);
        }

        private static Fixed ShoelaceArea(IReadOnlyList<StrokePoint> points)
        {
            Fixed sum = Fixed.Zero;
            int n = points.Count;
            for (int i = 0; i < n; i++)
            {
                StrokePoint p1 = points[i];
                StrokePoint p2 = points[(i + 1) % n];
                sum += p1.X * p2.Y - p2.X * p1.Y;
            }
            return Fixed.Abs(sum) / Fixed.FromInt(2);
        }

        private static Fixed Distance(StrokePoint a, StrokePoint b)
        {
            Fixed dx = b.X - a.X;
            Fixed dy = b.Y - a.Y;
            return Fixed.Sqrt(dx * dx + dy * dy);
        }

        private static Fixed DistancePointToSegment(StrokePoint p, StrokePoint a, StrokePoint b)
        {
            Fixed abx = b.X - a.X;
            Fixed aby = b.Y - a.Y;
            Fixed apx = p.X - a.X;
            Fixed apy = p.Y - a.Y;

            Fixed abLenSq = abx * abx + aby * aby;
            if (abLenSq.Raw == 0)
                return Distance(p, a);

            Fixed t = (apx * abx + apy * aby) / abLenSq;
            if (t.Raw < 0) t = Fixed.Zero;
            else if (t.Raw > Fixed.One.Raw) t = Fixed.One;

            Fixed closestX = a.X + abx * t;
            Fixed closestY = a.Y + aby * t;
            Fixed dx = p.X - closestX;
            Fixed dy = p.Y - closestY;
            return Fixed.Sqrt(dx * dx + dy * dy);
        }

        private static Fixed SegmentToSegmentDistance(StrokePoint a1, StrokePoint a2, StrokePoint b1, StrokePoint b2)
        {
            if (SegmentsIntersect(a1, a2, b1, b2))
                return Fixed.Zero;

            Fixed d1 = DistancePointToSegment(a1, b1, b2);
            Fixed d2 = DistancePointToSegment(a2, b1, b2);
            Fixed d3 = DistancePointToSegment(b1, a1, a2);
            Fixed d4 = DistancePointToSegment(b2, a1, a2);
            return Fixed.Min(Fixed.Min(d1, d2), Fixed.Min(d3, d4));
        }

        private static Fixed Cross(StrokePoint o, StrokePoint a, StrokePoint b)
            => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);

        private static bool OnSegment(StrokePoint a, StrokePoint b, StrokePoint p)
            => p.X.Raw >= Math.Min(a.X.Raw, b.X.Raw) && p.X.Raw <= Math.Max(a.X.Raw, b.X.Raw) &&
               p.Y.Raw >= Math.Min(a.Y.Raw, b.Y.Raw) && p.Y.Raw <= Math.Max(a.Y.Raw, b.Y.Raw);

        private static bool SegmentsIntersect(StrokePoint a1, StrokePoint a2, StrokePoint b1, StrokePoint b2)
        {
            Fixed d1 = Cross(b1, b2, a1);
            Fixed d2 = Cross(b1, b2, a2);
            Fixed d3 = Cross(a1, a2, b1);
            Fixed d4 = Cross(a1, a2, b2);

            if (((d1.Raw > 0 && d2.Raw < 0) || (d1.Raw < 0 && d2.Raw > 0)) &&
                ((d3.Raw > 0 && d4.Raw < 0) || (d3.Raw < 0 && d4.Raw > 0)))
                return true;

            if (d1.Raw == 0 && OnSegment(b1, b2, a1)) return true;
            if (d2.Raw == 0 && OnSegment(b1, b2, a2)) return true;
            if (d3.Raw == 0 && OnSegment(a1, a2, b1)) return true;
            if (d4.Raw == 0 && OnSegment(a1, a2, b2)) return true;

            return false;
        }
    }
}
