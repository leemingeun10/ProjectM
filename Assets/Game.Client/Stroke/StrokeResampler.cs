using System.Collections.Generic;
using UnityEngine;

namespace Game.Client.Stroke
{
    // Resamples a raw point list to an exact target count, evenly spaced by arc length.
    public static class StrokeResampler
    {
        public static List<Vector2> Resample(IReadOnlyList<Vector2> points, int targetCount)
        {
            var result = new List<Vector2>(targetCount);
            if (points == null || points.Count == 0 || targetCount < 2)
                return result;

            if (points.Count == 1)
            {
                for (int i = 0; i < targetCount; i++)
                    result.Add(points[0]);
                return result;
            }

            var cumulative = new float[points.Count];
            float totalLength = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                totalLength += Vector2.Distance(points[i - 1], points[i]);
                cumulative[i] = totalLength;
            }

            if (totalLength <= 0f)
            {
                for (int i = 0; i < targetCount; i++)
                    result.Add(points[0]);
                return result;
            }

            int segment = 0;
            for (int i = 0; i < targetCount; i++)
            {
                float targetDist = totalLength * i / (targetCount - 1);

                while (segment < cumulative.Length - 2 && cumulative[segment + 1] < targetDist)
                    segment++;

                float segStart = cumulative[segment];
                float segEnd = cumulative[segment + 1];
                float t = segEnd > segStart ? (targetDist - segStart) / (segEnd - segStart) : 0f;
                result.Add(Vector2.Lerp(points[segment], points[segment + 1], t));
            }

            return result;
        }
    }
}
