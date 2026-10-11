using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Ui
{
    // Full-rect vertical gradient (top colour to bottom colour) without a texture.
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIGradient : Graphic
    {
        [SerializeField] private Color topColor = Color.white;
        [SerializeField] private Color bottomColor = Color.black;

        public void SetColors(Color top, Color bottom)
        {
            topColor = top;
            bottomColor = bottom;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            UIVertex v = UIVertex.simpleVert;

            v.color = bottomColor * color;
            v.position = new Vector2(r.xMin, r.yMin);
            vh.AddVert(v);
            v.position = new Vector2(r.xMax, r.yMin);
            vh.AddVert(v);
            v.color = topColor * color;
            v.position = new Vector2(r.xMax, r.yMax);
            vh.AddVert(v);
            v.position = new Vector2(r.xMin, r.yMax);
            vh.AddVert(v);

            vh.AddTriangle(0, 2, 1);
            vh.AddTriangle(0, 3, 2);
        }
    }
}
