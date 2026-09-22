using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Stroke
{
    // Draws a polyline as a triangle-strip mesh on a UI Graphic, so it renders correctly
    // inside a Screen Space - Overlay canvas without needing a dynamic Texture2D.
    [RequireComponent(typeof(CanvasRenderer))]
    public class UILineRenderer : Graphic
    {
        [SerializeField] private float thickness = 6f;

        private readonly List<Vector2> points = new List<Vector2>();

        public void SetPoints(IReadOnlyList<Vector2> localPoints)
        {
            points.Clear();
            if (localPoints != null)
                points.AddRange(localPoints);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (points.Count < 2)
                return;

            for (int i = 0; i < points.Count - 1; i++)
                AddSegment(vh, points[i], points[i + 1], i * 4);
        }

        private void AddSegment(VertexHelper vh, Vector2 a, Vector2 b, int baseIndex)
        {
            Vector2 dir = (b - a).normalized;
            Vector2 normal = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;

            vertex.position = a - normal;
            vh.AddVert(vertex);
            vertex.position = a + normal;
            vh.AddVert(vertex);
            vertex.position = b + normal;
            vh.AddVert(vertex);
            vertex.position = b - normal;
            vh.AddVert(vertex);

            vh.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 2);
            vh.AddTriangle(baseIndex, baseIndex + 2, baseIndex + 3);
        }
    }
}
