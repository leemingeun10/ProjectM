using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Story
{
    // One stage marker (the prototype's marker()): circle with a slash for a battle (dashed ring
    // for the forced-loss battle), diamond for a story stage, double ring with an X for a boss;
    // filled ink once cleared, jade dot above the current stage. Drawn around the rect centre.
    [RequireComponent(typeof(CanvasRenderer))]
    public class StoryNodeMarker : Graphic
    {
        private StoryStageKind kind;
        private StoryStageState state;
        private float scale = 1f;

        public void Set(StoryStageKind stageKind, StoryStageState stageState, float viewScale)
        {
            kind = stageKind;
            state = stageState;
            scale = viewScale;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Vector2 o = rectTransform.rect.center;
            float s = scale;
            Color stroke = state == StoryStageState.Locked ? StoryMapPalette.InkSoft : StoryMapPalette.Ink;
            Color fill = state == StoryStageState.Cleared ? StoryMapPalette.Ink : StoryMapPalette.Paper;
            Color mark = state == StoryStageState.Cleared ? StoryMapPalette.Paper : stroke;
            float line = 2.5f * s;

            if (kind == StoryStageKind.Story)
            {
                float r = 11f * 1.4142f * s;
                var pts = new[] { o + new Vector2(0, r), o + new Vector2(r, 0), o + new Vector2(0, -r), o + new Vector2(-r, 0) };
                StoryMapMesh.Polygon(vh, pts, fill);
                for (int i = 0; i < 4; i++)
                    StoryMapMesh.Quad(vh, pts[i], pts[(i + 1) % 4], line, stroke);
                for (int i = 0; i < 4; i++)
                    StoryMapMesh.Disc(vh, pts[i], line * 0.5f, stroke, 6);
            }
            else if (kind == StoryStageKind.Boss)
            {
                StoryMapMesh.Disc(vh, o, 24f * s, StoryMapPalette.Paper, 40);
                StoryMapMesh.Ring(vh, o, 24f * s, 1.5f * s, stroke, 40, false);
                StoryMapMesh.Disc(vh, o, 17f * s, fill, 32);
                StoryMapMesh.Ring(vh, o, 17f * s, line, stroke, 32, false);
                float x = 7f * s;
                StoryMapMesh.Quad(vh, o + new Vector2(-x, -x), o + new Vector2(x, x), line, mark);
                StoryMapMesh.Quad(vh, o + new Vector2(-x, x), o + new Vector2(x, -x), line, mark);
            }
            else
            {
                StoryMapMesh.Disc(vh, o, 15f * s, fill, 32);
                StoryMapMesh.Ring(vh, o, 15f * s, line, stroke, 32, kind == StoryStageKind.Loss);
                float x = 6f * s;
                StoryMapMesh.Quad(vh, o + new Vector2(-x, -x), o + new Vector2(x, x), line, mark);
            }

            if (state == StoryStageState.Current)
            {
                float top = (kind == StoryStageKind.Boss ? 26f : 18f) * s;
                Vector2 dot = o + new Vector2(0, top);
                StoryMapMesh.Quad(vh, dot, dot + new Vector2(-14f, 9f) * s, 5f * s, StoryMapPalette.Jade);
                StoryMapMesh.Quad(vh, dot, dot + new Vector2(16f, 11f) * s, 5f * s, StoryMapPalette.Jade);
                StoryMapMesh.Disc(vh, dot, 4.5f * s, StoryMapPalette.Jade, 12);
            }
        }
    }
}
