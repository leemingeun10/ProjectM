using System.Collections.Generic;
using Game.Sim.Stroke;
using UnityEngine;

namespace Game.Client.Stroke
{
    // Maps a recorded stroke into the unit square for small previews. Each stroke is fitted to
    // its own bounding box with a uniform scale (the longer side spans 0..1, the shorter one is
    // centred), so the shape is never distorted and the stroke always fills its preview, whatever
    // space it was recorded in (player strokes are frame centimetres, AI strokes canvas units).
    public static class StrokeFitter
    {
        public static List<Vector2> FitToUnitSquare(IReadOnlyList<StrokePoint> points)
        {
            int count = points?.Count ?? 0;
            var result = new List<Vector2>(count);
            if (count == 0)
                return result;

            // Boundary conversion: Game.sim fixed-point -> float, display only.
            var raw = new Vector2[count];
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < count; i++)
            {
                raw[i] = new Vector2(points[i].X.Raw / (float)Fixed.Scale, points[i].Y.Raw / (float)Fixed.Scale);
                min = Vector2.Min(min, raw[i]);
                max = Vector2.Max(max, raw[i]);
            }

            Vector2 size = max - min;
            float extent = Mathf.Max(size.x, size.y);
            if (extent <= 0f)
            {
                // A dot (all points equal): centre it rather than dividing by zero.
                for (int i = 0; i < count; i++)
                    result.Add(new Vector2(0.5f, 0.5f));
                return result;
            }

            Vector2 offset = (new Vector2(extent, extent) - size) * 0.5f;
            for (int i = 0; i < count; i++)
                result.Add((raw[i] - min + offset) / extent);
            return result;
        }
    }
}
