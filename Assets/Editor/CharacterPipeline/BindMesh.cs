using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CharacterPipeline
{
    // The rigged model's skinned mesh in its bind pose, in model space (root at the origin, no
    // root scale), with each vertex's height as a fraction of the model height from the soles.
    internal sealed class BindMesh
    {
        public UnityEngine.Mesh source;
        public Vector3[] positions;
        public Vector2[] uv;
        public int[] triangles;
        public BoneWeight[] weights;
        public string[] boneNames;
        public float minY, maxY;

        public float Height => maxY - minY;
        public float HeightFraction(Vector3 p) => (p.y - minY) / Height;

        public static BindMesh Load(GameObject modelPrefab)
        {
            var instance = Object.Instantiate(modelPrefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            try
            {
                var smr = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var baked = new UnityEngine.Mesh();
                smr.BakeMesh(baked, false);
                Matrix4x4 toModel = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                var result = new BindMesh
                {
                    source = smr.sharedMesh,
                    uv = smr.sharedMesh.uv,
                    triangles = smr.sharedMesh.triangles,
                    weights = smr.sharedMesh.boneWeights,
                    boneNames = new string[smr.bones.Length],
                };
                for (int i = 0; i < smr.bones.Length; i++)
                    result.boneNames[i] = smr.bones[i] != null ? smr.bones[i].name : "";
                Vector3[] verts = baked.vertices;
                result.positions = new Vector3[verts.Length];
                result.minY = float.MaxValue;
                result.maxY = float.MinValue;
                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 p = toModel.MultiplyPoint3x4(verts[i]);
                    result.positions[i] = p;
                    result.minY = Mathf.Min(result.minY, p.y);
                    result.maxY = Mathf.Max(result.maxY, p.y);
                }
                Object.DestroyImmediate(baked);
                return result;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        public static string TexturePath(Material material, string property)
        {
            Texture t = material != null && material.HasProperty(property) ? material.GetTexture(property) : null;
            return t != null ? AssetDatabase.GetAssetPath(t) : null;
        }

        // Full-resolution, readable copy of a texture file regardless of its import settings.
        public static Texture2D ReadPixelsFromFile(string assetPath)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(System.IO.File.ReadAllBytes(System.IO.Path.GetFullPath(assetPath)));
            return texture;
        }

        // Calls `visit(x, y)` for every texel covered by triangle `tri` in UV space (with a small dilation
        // so painted islands don't leave seams).
        public void RasterizeTriangle(int tri, int width, int height, int dilatePixels, System.Action<int, int> visit)
        {
            Vector2 a = Vector2.Scale(uv[triangles[tri * 3]], new Vector2(width, height));
            Vector2 b = Vector2.Scale(uv[triangles[tri * 3 + 1]], new Vector2(width, height));
            Vector2 c = Vector2.Scale(uv[triangles[tri * 3 + 2]], new Vector2(width, height));
            Vector2 centre = (a + b + c) / 3f;
            // Grow the triangle around its centre by `dilatePixels`.
            System.Func<Vector2, Vector2> grow = p =>
            {
                Vector2 d = p - centre;
                float len = d.magnitude;
                return len < 1e-5f ? p : p + d / len * dilatePixels;
            };
            a = grow(a);
            b = grow(b);
            c = grow(c);
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
            int x1 = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
            int y1 = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
            float area = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);
            if (Mathf.Abs(area) < 1e-6f)
                return;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = ((b.x - p.x) * (c.y - p.y) - (c.x - p.x) * (b.y - p.y)) / area;
                    float w1 = ((c.x - p.x) * (a.y - p.y) - (a.x - p.x) * (c.y - p.y)) / area;
                    float w2 = 1f - w0 - w1;
                    if (w0 >= 0f && w1 >= 0f && w2 >= 0f)
                        visit(x, y);
                }
            }
        }

        public Vector3 TriangleCentroid(int tri)
        {
            return (positions[triangles[tri * 3]] + positions[triangles[tri * 3 + 1]] + positions[triangles[tri * 3 + 2]]) / 3f;
        }

        public Vector2 TriangleUvCentroid(int tri)
        {
            return (uv[triangles[tri * 3]] + uv[triangles[tri * 3 + 1]] + uv[triangles[tri * 3 + 2]]) / 3f;
        }

        public int TriangleCount => triangles.Length / 3;

        public HashSet<int> BoneSubtreeIndices(GameObject modelPrefab, IEnumerable<string> rootBoneNames)
        {
            var names = new HashSet<string>();
            var roots = new HashSet<string>(rootBoneNames);
            foreach (Transform t in modelPrefab.GetComponentsInChildren<Transform>(true))
            {
                if (!roots.Contains(t.name))
                    continue;
                foreach (Transform d in t.GetComponentsInChildren<Transform>(true))
                    names.Add(d.name);
            }
            var indices = new HashSet<int>();
            for (int i = 0; i < boneNames.Length; i++)
            {
                if (names.Contains(boneNames[i]))
                    indices.Add(i);
            }
            return indices;
        }
    }
}
