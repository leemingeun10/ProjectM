using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Story
{
    // The road between a chapter's nodes: one S-curve per pair (the prototype's seg()), solid ink
    // once the lower stage is cleared, dotted wash otherwise. Points are local to this rect.
    [RequireComponent(typeof(CanvasRenderer))]
    public class StoryMapPaths : Graphic
    {
        private const int SamplesPerSegment = 32;

        private readonly List<(Vector2 from, Vector2 to, bool done)> segments = new List<(Vector2, Vector2, bool)>();
        private float scale = 1f;

        // scale = canvas units per prototype viewBox unit, for line widths and dash lengths.
        public void SetSegments(IEnumerable<(Vector2 from, Vector2 to, bool done)> roads, float viewScale)
        {
            segments.Clear();
            segments.AddRange(roads);
            scale = viewScale;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            foreach ((Vector2 a, Vector2 b, bool done) in segments)
            {
                var path = new List<Vector2>(SamplesPerSegment + 1);
                float my = (a.y + b.y) * 0.5f;
                Vector2 c1 = new Vector2(a.x, my), c2 = new Vector2(b.x, my);
                for (int i = 0; i <= SamplesPerSegment; i++)
                {
                    float t = (float)i / SamplesPerSegment, u = 1f - t;
                    path.Add(u * u * u * a + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * b);
                }

                if (done)
                    AddPolyline(vh, path, 6f * scale, StoryMapPalette.Ink);
                else
                    AddDots(vh, path, 2.5f * scale, 14f * scale, StoryMapPalette.Wash);
            }
        }

        private static void AddPolyline(VertexHelper vh, List<Vector2> path, float width, Color c)
        {
            for (int i = 0; i < path.Count - 1; i++)
                StoryMapMesh.Quad(vh, path[i], path[i + 1], width, c);
            for (int i = 1; i < path.Count - 1; i++)
                StoryMapMesh.Disc(vh, path[i], width * 0.5f, c, 8);
        }

        // Round dots every `spacing` along the curve.
        private static void AddDots(VertexHelper vh, List<Vector2> path, float radius, float spacing, Color c)
        {
            float carry = 0f;
            StoryMapMesh.Disc(vh, path[0], radius, c, 10);
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector2 p = path[i], q = path[i + 1];
                float len = Vector2.Distance(p, q);
                float d = spacing - carry;
                while (d <= len)
                {
                    StoryMapMesh.Disc(vh, Vector2.Lerp(p, q, d / len), radius, c, 10);
                    d += spacing;
                }
                carry = len - (d - spacing);
            }
        }
    }
}
