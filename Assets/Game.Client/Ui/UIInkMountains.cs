using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Ui
{
    // Layered ink-wash mountain ridges (and an optional sun) drawn as UI geometry, for screen
    // backgrounds that need the 수묵 mood without an image. Each ridge is a soft sum of sines;
    // the far layers sit higher and fainter, and each layer fades into the one behind it at its
    // crest, like ink bleeding into wet paper.
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIInkMountains : Graphic
    {
        [Serializable]
        public struct Ridge
        {
            [Tooltip("Base height of the ridge as a fraction of the rect height.")]
            public float height;
            [Tooltip("Ridge undulation as a fraction of the rect height.")]
            public float amplitude;
            public float phase;
            public Color color;
        }

        private const int Segments = 72;

        [SerializeField] private Ridge[] ridges =
        {
            new Ridge { height = 0.42f, amplitude = 0.08f, phase = 3.9f, color = new Color(0.25f, 0.24f, 0.23f, 0.35f) },
            new Ridge { height = 0.30f, amplitude = 0.07f, phase = 1.7f, color = new Color(0.18f, 0.18f, 0.17f, 0.6f) },
            new Ridge { height = 0.17f, amplitude = 0.05f, phase = 0.4f, color = new Color(0.08f, 0.08f, 0.08f, 0.95f) },
        };
        [Tooltip("Height (fraction of the ridge's own crest) over which the crest fades out.")]
        [Range(0f, 1f)]
        [SerializeField] private float crestSoftness = 0.25f;

        [Header("Sun")]
        [SerializeField] private bool showSun = true;
        [SerializeField] private Vector2 sunPosition = new Vector2(0.3f, 0.55f);
        [SerializeField] private float sunRadius = 0.06f;
        [SerializeField] private Color sunColor = new Color(0.784f, 0.263f, 0.165f, 0.9f);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;

            // The sun sits behind every ridge, so it is drawn first.
            if (showSun)
                AddDisc(vh, new Vector2(r.xMin + r.width * sunPosition.x, r.yMin + r.height * sunPosition.y), r.width * sunRadius, sunColor * color);

            foreach (Ridge ridge in ridges)
                AddRidge(vh, r, ridge);
        }

        private void AddRidge(VertexHelper vh, Rect r, Ridge ridge)
        {
            Color solid = ridge.color * color;
            Color clear = solid;
            clear.a = 0f;

            for (int s = 0; s <= Segments; s++)
            {
                float u = s / (float)Segments;
                float x = r.xMin + r.width * u;
                float z = u * 18f;
                float wave = Mathf.Sin(z * 0.55f + ridge.phase) * 0.55f
                           + Mathf.Sin(z * 1.37f + ridge.phase * 2.3f) * 0.3f
                           + Mathf.Sin(z * 3.1f + ridge.phase * 0.7f) * 0.15f;
                float crest = r.yMin + r.height * Mathf.Max(0.01f, ridge.height + wave * ridge.amplitude);
                float soft = r.yMin + (crest - r.yMin) * (1f - crestSoftness);

                int baseIndex = vh.currentVertCount;
                UIVertex v = UIVertex.simpleVert;
                v.color = solid;
                v.position = new Vector2(x, r.yMin);
                vh.AddVert(v);
                v.position = new Vector2(x, soft);
                vh.AddVert(v);
                v.color = Color.Lerp(clear, solid, 0.85f);
                v.position = new Vector2(x, crest);
                vh.AddVert(v);

                if (s == 0)
                    continue;
                int prev = baseIndex - 3;
                vh.AddTriangle(prev, prev + 1, baseIndex + 1);
                vh.AddTriangle(prev, baseIndex + 1, baseIndex);
                vh.AddTriangle(prev + 1, prev + 2, baseIndex + 2);
                vh.AddTriangle(prev + 1, baseIndex + 2, baseIndex + 1);
            }
        }

        private static void AddDisc(VertexHelper vh, Vector2 centre, float radius, Color fill)
        {
            const int sides = 48;
            Color halo = fill;
            halo.a *= 0.18f;
            Color clear = fill;
            clear.a = 0f;

            // Solid disc plus a soft halo ring around it.
            int centreIndex = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;
            v.color = fill;
            v.position = centre;
            vh.AddVert(v);
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                v.color = fill;
                v.position = centre + dir * radius;
                vh.AddVert(v);
                v.color = halo;
                v.position = centre + dir * radius * 1.04f;
                vh.AddVert(v);
                v.color = clear;
                v.position = centre + dir * radius * 2.4f;
                vh.AddVert(v);

                if (i == 0)
                    continue;
                int cur = centreIndex + 1 + i * 3;
                int prev = cur - 3;
                vh.AddTriangle(centreIndex, prev, cur);
                vh.AddTriangle(prev, prev + 1, cur + 1);
                vh.AddTriangle(prev, cur + 1, cur);
                vh.AddTriangle(prev + 1, prev + 2, cur + 2);
                vh.AddTriangle(prev + 1, cur + 2, cur + 1);
            }
        }
    }
}
