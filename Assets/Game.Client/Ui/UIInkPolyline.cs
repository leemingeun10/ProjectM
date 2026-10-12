using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Ui
{
    // A thin brush line along an arbitrary path: full width where the brush lands, thinning and
    // drying toward the tail, with a small dot marking where the stroke started.
    // Points are normalized (0..1 on both axes, y up) and mapped into the largest centred square
    // that fits the rect, so the same data stays undistorted at any rect size.
    // Reveal (0..1) draws only the leading part of the path, for a "being drawn" animation.
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIInkPolyline : Graphic
    {
        private const int DotSegments = 10;

        [SerializeField] private float thickness = 5f;
        [SerializeField] private float padding = 14f;
        [SerializeField] private float startDotRadius = 5f;
        [SerializeField] private Color startDotColor = InkTheme.Vermilion;

        private readonly List<Vector2> points = new List<Vector2>();
        private float reveal = 1f;

        public void SetPoints(IReadOnlyList<Vector2> normalizedPoints)
        {
            points.Clear();
            if (normalizedPoints != null)
                points.AddRange(normalizedPoints);
            SetVerticesDirty();
        }

        public float Reveal
        {
            get => reveal;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(clamped, reveal))
                    return;
                reveal = clamped;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (points.Count == 0)
                return;

            Rect r = rectTransform.rect;
            float side = Mathf.Max(0f, Mathf.Min(r.width, r.height) - 2f * padding);
            Vector2 origin = r.center - new Vector2(side, side) * 0.5f;

            // Visible prefix: whole points up to the reveal position plus one interpolated tip.
            float head = reveal * (points.Count - 1);
            int whole = Mathf.FloorToInt(head);
            var path = new List<Vector2>(whole + 2);
            for (int i = 0; i <= whole && i < points.Count; i++)
                path.Add(origin + points[i] * side);
            if (whole < points.Count - 1 && head > whole)
                path.Add(origin + Vector2.Lerp(points[whole], points[whole + 1], head - whole) * side);

            if (path.Count >= 2)
                AddBrush(vh, path);
            AddDot(vh, path[0]);
        }

        // One continuous strip: each vertex pair sits on the averaged normal of its neighbours,
        // so corners stay closed instead of showing the gaps of per-segment quads.
        private void AddBrush(VertexHelper vh, List<Vector2> path)
        {
            Color faded = color;
            faded.a *= 0.55f;
            int n = path.Count;
            int baseIndex = vh.currentVertCount;

            for (int i = 0; i < n; i++)
            {
                Vector2 prev = path[Mathf.Max(0, i - 1)];
                Vector2 next = path[Mathf.Min(n - 1, i + 1)];
                Vector2 dir = next - prev;
                dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
                Vector2 normal = new Vector2(-dir.y, dir.x);

                // u is measured along the whole stroke so the taper doesn't shift while revealing.
                float u = points.Count > 1 ? (float)i / (points.Count - 1) : 0f;
                float attack = Mathf.Clamp01(0.55f + u * 8f);
                float tail = Mathf.Lerp(1f, 0.35f, u * u);
                float half = Mathf.Max(0.4f, thickness * 0.5f * attack * tail);

                UIVertex v = UIVertex.simpleVert;
                v.color = Color.Lerp(color, faded, u);
                v.position = path[i] - normal * half;
                vh.AddVert(v);
                v.position = path[i] + normal * half;
                vh.AddVert(v);

                if (i == 0)
                    continue;
                int k = baseIndex + i * 2;
                vh.AddTriangle(k - 2, k - 1, k + 1);
                vh.AddTriangle(k - 2, k + 1, k);
            }
        }

        private void AddDot(VertexHelper vh, Vector2 center)
        {
            int baseIndex = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;
            v.color = startDotColor;
            v.position = center;
            vh.AddVert(v);
            for (int s = 0; s < DotSegments; s++)
            {
                float angle = 2f * Mathf.PI * s / DotSegments;
                v.position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * startDotRadius;
                vh.AddVert(v);
            }
            for (int s = 0; s < DotSegments; s++)
                vh.AddTriangle(baseIndex, baseIndex + 1 + s, baseIndex + 1 + (s + 1) % DotSegments);
        }
    }
}
