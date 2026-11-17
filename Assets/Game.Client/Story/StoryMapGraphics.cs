using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Story
{
    // Colours of the story map, taken from the prototype's light (paper) palette.
    public static class StoryMapPalette
    {
        public static readonly Color Paper = Hex(0xE7E5E0);
        public static readonly Color Ink = Hex(0x262A2E);
        public static readonly Color InkSoft = Hex(0x646B71);
        public static readonly Color Wash = Hex(0xC6C7C4);
        public static readonly Color Slate = Hex(0x5E7080);
        public static readonly Color Ochre = Hex(0xA98D58);
        public static readonly Color Jade = Hex(0x0E9C84);

        private static Color Hex(int rgb)
            => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }

    // Small mesh helpers shared by the two map graphics.
    internal static class StoryMapMesh
    {
        public static void Quad(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
        {
            Vector2 dir = b - a;
            if (dir.sqrMagnitude < 1e-6f)
                return;
            Vector2 n = new Vector2(-dir.y, dir.x).normalized * (width * 0.5f);
            int i = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;
            v.color = c;
            v.position = a - n; vh.AddVert(v);
            v.position = a + n; vh.AddVert(v);
            v.position = b + n; vh.AddVert(v);
            v.position = b - n; vh.AddVert(v);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        public static void Disc(VertexHelper vh, Vector2 center, float radius, Color c, int segments)
        {
            int i = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;
            v.color = c;
            v.position = center;
            vh.AddVert(v);
            for (int k = 0; k < segments; k++)
            {
                float a = 2f * Mathf.PI * k / segments;
                v.position = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                vh.AddVert(v);
            }
            for (int k = 0; k < segments; k++)
                vh.AddTriangle(i, i + 1 + k, i + 1 + (k + 1) % segments);
        }

        // A ring of the given line width; dashed draws every other pair of segments.
        public static void Ring(VertexHelper vh, Vector2 center, float radius, float width, Color c, int segments, bool dashed)
        {
            float rIn = radius - width * 0.5f, rOut = radius + width * 0.5f;
            UIVertex v = UIVertex.simpleVert;
            v.color = c;
            for (int k = 0; k < segments; k++)
            {
                if (dashed && (k / 2) % 2 == 1)
                    continue;
                float a0 = 2f * Mathf.PI * k / segments, a1 = 2f * Mathf.PI * (k + 1) / segments;
                Vector2 d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)), d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
                int i = vh.currentVertCount;
                v.position = center + d0 * rIn; vh.AddVert(v);
                v.position = center + d0 * rOut; vh.AddVert(v);
                v.position = center + d1 * rOut; vh.AddVert(v);
                v.position = center + d1 * rIn; vh.AddVert(v);
                vh.AddTriangle(i, i + 1, i + 2);
                vh.AddTriangle(i, i + 2, i + 3);
            }
        }

        // Convex polygon fan.
        public static void Polygon(VertexHelper vh, Vector2[] points, Color c)
        {
            int i = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;
            v.color = c;
            foreach (Vector2 p in points)
            {
                v.position = p;
                vh.AddVert(v);
            }
            for (int k = 1; k < points.Length - 1; k++)
                vh.AddTriangle(i, i + k, i + k + 1);
        }
    }
}
