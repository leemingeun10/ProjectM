using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Ui
{
    // A horizontal divider shaped like a single brush stroke: thick in the middle, tapering to
    // dry ends, with a slight deterministic wobble. Fills the rect's width and is centred on its height.
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIInkStroke : Graphic
    {
        private const int Segments = 40;

        [SerializeField] private float thickness = 6f;
        [SerializeField] private float wobble = 1.2f;
        [SerializeField] private float seed = 1.3f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float midY = r.center.y;
            Color faded = color;
            faded.a *= 0.55f;

            for (int s = 0; s <= Segments; s++)
            {
                float u = s / (float)Segments;
                float x = Mathf.Lerp(r.xMin, r.xMax, u);
                // Fast attack on the left, long dry tail on the right, like a real stroke.
                float taper = Mathf.Sin(Mathf.Pow(u, 0.8f) * Mathf.PI);
                float pressure = 0.75f + 0.25f * Mathf.Sin(u * 9f + seed * 3f);
                float half = Mathf.Max(0.3f, thickness * 0.5f * taper * pressure);
                float y = midY + Mathf.Sin(u * 5.3f + seed) * wobble;

                UIVertex v = UIVertex.simpleVert;
                v.color = Color.Lerp(color, faded, Mathf.Abs(u - 0.4f));
                v.position = new Vector2(x, y - half);
                vh.AddVert(v);
                v.position = new Vector2(x, y + half);
                vh.AddVert(v);

                if (s == 0)
                    continue;
                int i = s * 2;
                vh.AddTriangle(i - 2, i - 1, i + 1);
                vh.AddTriangle(i - 2, i + 1, i);
            }
        }
    }
}
