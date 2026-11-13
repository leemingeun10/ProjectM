using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CharacterPipeline
{
    // Step 8: finds the channel through the right fist (the hole a sword grip goes through) in the
    // bind T-pose. Rays are cast along the body's forward axis over a grid across the fist; grid
    // cells that no ray hits but that are enclosed by the fist on all four sides form a through
    // hole. Without one, the thinnest enclosed spot (a dent) is used instead.
    public static class SwordSocket
    {
        public struct Result
        {
            public bool found;
            public bool through;
            public Vector3 centre;      // world
            public int holeCells;
            public float minThickness;  // metres along forward at the chosen spot
            public string report;
        }

        // Only the fist counts: hand vertices further along the arm than `fistStart` of the hand's
        // length past the wrist pivot (the generated hand bone also carries the wrist and cuff).
        public static Result MeasureFistHole(SkinnedMeshRenderer smr, Transform hand, Transform lowerArm, Vector3 forward, float step, float fistStart = 0.3f)
        {
            var baked = new UnityEngine.Mesh();
            smr.BakeMesh(baked, false);
            Matrix4x4 toWorld = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            Vector3[] verts = baked.vertices;
            for (int i = 0; i < verts.Length; i++)
                verts[i] = toWorld.MultiplyPoint3x4(verts[i]);
            int[] tris = smr.sharedMesh.triangles;
            BoneWeight[] bw = smr.sharedMesh.boneWeights;
            int handIndex = System.Array.IndexOf(smr.bones, hand);
            Object.DestroyImmediate(baked);

            // Hand vertices = dominated by the hand bone (the generated fists have no finger bones).
            var isHand = new bool[verts.Length];
            Vector3 armDir = (hand.position - lowerArm.position).normalized;
            float reach = 0f;
            for (int i = 0; i < verts.Length; i++)
            {
                if (bw[i].boneIndex0 == handIndex && bw[i].weight0 >= 0.5f)
                    reach = Mathf.Max(reach, Vector3.Dot(verts[i] - hand.position, armDir));
            }
            for (int i = 0; i < verts.Length; i++)
            {
                isHand[i] = bw[i].boneIndex0 == handIndex && bw[i].weight0 >= 0.5f
                            && Vector3.Dot(verts[i] - hand.position, armDir) >= fistStart * reach;
            }
            var handTris = new List<int>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                if (isHand[tris[t]] || isHand[tris[t + 1]] || isHand[tris[t + 2]])
                    handTris.Add(t);
            }

            // Grid axes: across the fist (u = arm direction projected, v = up), rays along forward.
            Vector3 f = forward.normalized;
            Vector3 v = Vector3.ProjectOnPlane(Vector3.up, f).normalized;
            Vector3 u = Vector3.Cross(v, f).normalized;
            float uMin = float.MaxValue, uMax = float.MinValue, vMin = float.MaxValue, vMax = float.MinValue, fMin = float.MaxValue, fMax = float.MinValue;
            Vector3 origin = hand.position;
            for (int i = 0; i < verts.Length; i++)
            {
                if (!isHand[i])
                    continue;
                Vector3 d = verts[i] - origin;
                uMin = Mathf.Min(uMin, Vector3.Dot(d, u)); uMax = Mathf.Max(uMax, Vector3.Dot(d, u));
                vMin = Mathf.Min(vMin, Vector3.Dot(d, v)); vMax = Mathf.Max(vMax, Vector3.Dot(d, v));
                fMin = Mathf.Min(fMin, Vector3.Dot(d, f)); fMax = Mathf.Max(fMax, Vector3.Dot(d, f));
            }
            int nu = Mathf.Max(1, Mathf.CeilToInt((uMax - uMin) / step));
            int nv = Mathf.Max(1, Mathf.CeilToInt((vMax - vMin) / step));
            var hits = new int[nu, nv];
            var thickness = new float[nu, nv];
            var midDepth = new float[nu, nv];
            float start = fMin - 0.05f;
            for (int i = 0; i < nu; i++)
            {
                for (int j = 0; j < nv; j++)
                {
                    Vector3 p = origin + u * (uMin + (i + 0.5f) * step) + v * (vMin + (j + 0.5f) * step) + f * start;
                    float first = float.MaxValue, last = float.MinValue;
                    int n = 0;
                    foreach (int t in handTris)
                    {
                        float d = PipelineMath.RayTriangle(p, f, verts[tris[t]], verts[tris[t + 1]], verts[tris[t + 2]]);
                        if (d < 0f)
                            continue;
                        n++;
                        first = Mathf.Min(first, d);
                        last = Mathf.Max(last, d);
                    }
                    hits[i, j] = n;
                    // A single hit is a grazing ray or an open edge, not a measurable thickness.
                    thickness[i, j] = n >= 2 ? last - first : float.MaxValue;
                    midDepth[i, j] = n > 0 ? start + (first + last) * 0.5f : 0f;
                }
            }

            // Through-hole cells: no hit, but fist cells on all four sides along the grid lines.
            var hole = new List<Vector2Int>();
            for (int i = 0; i < nu; i++)
            {
                for (int j = 0; j < nv; j++)
                {
                    if (hits[i, j] > 0)
                        continue;
                    if (Enclosed(hits, i, j, nu, nv))
                        hole.Add(new Vector2Int(i, j));
                }
            }

            var result = new Result();
            if (hole.Count > 0)
            {
                Vector2 c = Vector2.zero;
                foreach (Vector2Int h in hole)
                    c += new Vector2(h.x, h.y);
                c /= hole.Count;
                result.found = true;
                result.through = true;
                result.holeCells = hole.Count;
                result.centre = origin + u * (uMin + (c.x + 0.5f) * step) + v * (vMin + (c.y + 0.5f) * step) + f * ((fMin + fMax) * 0.5f);
            }
            else
            {
                // Dent: thinnest enclosed cell (ignore the silhouette border).
                float best = float.MaxValue;
                Vector2Int bestCell = default;
                for (int i = 1; i < nu - 1; i++)
                {
                    for (int j = 1; j < nv - 1; j++)
                    {
                        if (hits[i, j] == 0 || !Enclosed(hits, i, j, nu, nv))
                            continue;
                        if (thickness[i, j] < best)
                        {
                            best = thickness[i, j];
                            bestCell = new Vector2Int(i, j);
                        }
                    }
                }
                if (best < float.MaxValue)
                {
                    result.found = true;
                    result.minThickness = best;
                    result.centre = origin + u * (uMin + (bestCell.x + 0.5f) * step) + v * (vMin + (bestCell.y + 0.5f) * step) + f * midDepth[bestCell.x, bestCell.y];
                }
            }

            var sb = new StringBuilder();
            sb.Append($"Fist hole: grid {nu}x{nv} @ {step * 1000f:F1} mm, fist extent across {(uMax - uMin) * 100f:F1} cm, up {(vMax - vMin) * 100f:F1} cm, along forward {(fMax - fMin) * 100f:F1} cm\n");
            if (!result.found)
                sb.Append(" no hole or dent found\n");
            else if (result.through)
                sb.Append($" through hole: {hole.Count} cells (~{hole.Count * step * step * 1e4f:F2} cm²)\n");
            else
                sb.Append($" no through hole; thinnest enclosed spot {result.minThickness * 100f:F2} cm thick of {(fMax - fMin) * 100f:F1} cm fist depth (dent)\n");
            if (result.found)
            {
                Vector3 local = hand.InverseTransformPoint(result.centre);
                sb.Append($" centre (world, rel. hand) {(result.centre - origin) * 100f} cm, hand-local {local}\n");
            }
            result.report = sb.ToString();
            return result;
        }

        static bool Enclosed(int[,] hits, int i, int j, int nu, int nv)
        {
            bool left = false, right = false, down = false, up = false;
            for (int k = i - 1; k >= 0 && !left; k--) left = hits[k, j] > 0;
            for (int k = i + 1; k < nu && !right; k++) right = hits[k, j] > 0;
            for (int k = j - 1; k >= 0 && !down; k--) down = hits[i, k] > 0;
            for (int k = j + 1; k < nv && !up; k++) up = hits[i, k] > 0;
            return left && right && down && up;
        }
    }
}
