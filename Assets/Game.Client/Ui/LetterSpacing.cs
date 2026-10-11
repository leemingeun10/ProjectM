using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Ui
{
    // Widens the gap between glyphs of a legacy UI Text, for the engraved look of titles and
    // button labels. Works on the generated quads (rich-text tags produce none), splitting lines
    // where a quad starts left of the previous one, and re-aligns each line to the Text alignment.
    [RequireComponent(typeof(Text))]
    public class LetterSpacing : BaseMeshEffect
    {
        [Tooltip("Extra space between glyphs, in canvas units.")]
        [SerializeField] private float spacing = 6f;

        private readonly List<UIVertex> verts = new List<UIVertex>();

        public float Spacing
        {
            get => spacing;
            set
            {
                spacing = value;
                if (graphic != null)
                    graphic.SetVerticesDirty();
            }
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || Mathf.Approximately(spacing, 0f))
                return;

            verts.Clear();
            vh.GetUIVertexStream(verts);
            int quadCount = verts.Count / 6;
            if (quadCount < 2)
                return;

            TextAnchor anchor = ((Text)graphic).alignment;
            float alignFactor = anchor == TextAnchor.UpperCenter || anchor == TextAnchor.MiddleCenter || anchor == TextAnchor.LowerCenter ? 0.5f
                : anchor == TextAnchor.UpperRight || anchor == TextAnchor.MiddleRight || anchor == TextAnchor.LowerRight ? 1f
                : 0f;

            int lineStart = 0;
            float previousMinX = float.MinValue;
            for (int q = 0; q <= quadCount; q++)
            {
                bool newLine = q == quadCount || QuadMinX(q) < previousMinX - 0.01f;
                if (newLine)
                {
                    ShiftLine(lineStart, q, alignFactor);
                    lineStart = q;
                }
                if (q < quadCount)
                    previousMinX = QuadMinX(q);
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(verts);
        }

        private float QuadMinX(int quad)
        {
            float min = float.MaxValue;
            for (int i = 0; i < 6; i++)
                min = Mathf.Min(min, verts[quad * 6 + i].position.x);
            return min;
        }

        private void ShiftLine(int firstQuad, int endQuad, float alignFactor)
        {
            int count = endQuad - firstQuad;
            float lineOffset = -(count - 1) * spacing * alignFactor;
            for (int q = firstQuad; q < endQuad; q++)
            {
                float dx = (q - firstQuad) * spacing + lineOffset;
                for (int i = 0; i < 6; i++)
                {
                    UIVertex v = verts[q * 6 + i];
                    v.position.x += dx;
                    verts[q * 6 + i] = v;
                }
            }
        }
    }
}
