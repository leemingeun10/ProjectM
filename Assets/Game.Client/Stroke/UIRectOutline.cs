using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Stroke
{
    // Draws only the border of its RectTransform (four quads, drawn inside the rect),
    // so the area it frames stays see-through.
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIRectOutline : Graphic
    {
        [SerializeField] private float thickness = 4f;

        public float Thickness
        {
            get => thickness;
            set
            {
                thickness = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float t = Mathf.Min(thickness, r.width * 0.5f, r.height * 0.5f);
            if (t <= 0f)
                return;

            AddQuad(vh, new Vector2(r.xMin, r.yMax - t), new Vector2(r.xMax, r.yMax));     // top
            AddQuad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin + t));     // bottom
            AddQuad(vh, new Vector2(r.xMin, r.yMin + t), new Vector2(r.xMin + t, r.yMax - t)); // left
            AddQuad(vh, new Vector2(r.xMax - t, r.yMin + t), new Vector2(r.xMax, r.yMax - t)); // right
        }

        private void AddQuad(VertexHelper vh, Vector2 min, Vector2 max)
        {
            int baseIndex = vh.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;

            vertex.position = new Vector2(min.x, min.y);
            vh.AddVert(vertex);
            vertex.position = new Vector2(min.x, max.y);
            vh.AddVert(vertex);
            vertex.position = new Vector2(max.x, max.y);
            vh.AddVert(vertex);
            vertex.position = new Vector2(max.x, min.y);
            vh.AddVert(vertex);

            vh.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 2);
            vh.AddTriangle(baseIndex, baseIndex + 2, baseIndex + 3);
        }
    }
}
