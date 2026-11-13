using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CharacterPipeline
{
    // Step 6 (mesh fixes, written to a mesh copy):
    //  - seam weld: split UV-seam vertices get one shared weight set, so the skin doesn't crack;
    //  - hem: a jacket hem that hangs over both thighs is auto-weighted to the thigh bones, so the
    //    cloth between the legs stretches when they part. Part of that leg weight moves to the hips
    //    bone: all of it at the hem top (and on the belt), `hemBottomHipShare` at the hem bottom.
    public static class HemWeights
    {
        public static string MeshPath(CharacterPipelineConfig config) => MaterialStandard.OutputFolder(config) + "/" + config.outputName + "_Mesh.asset";

        public static string Run(CharacterPipelineConfig config)
        {
            string fbx = HumanoidSetup.ModelPath(config);
            HumanBone[] human = HumanoidSetup.LoadAvatar(fbx).humanDescription.human;
            string hipsName = human.First(h => h.humanName == "Hips").boneName;
            string[] legRoots = human.Where(h => h.humanName == "LeftUpperLeg" || h.humanName == "RightUpperLeg").Select(h => h.boneName).ToArray();

            BindMesh mesh = BindMesh.Load(config.modelPrefab);
            int hips = System.Array.IndexOf(mesh.boneNames, hipsName);
            HashSet<int> leg = mesh.BoneSubtreeIndices(config.modelPrefab, legRoots);
            var smr = config.modelPrefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            string colorPath = BindMesh.TexturePath(smr.sharedMaterial, "_BaseMap") ?? BindMesh.TexturePath(smr.sharedMaterial, "_MainTex");
            Texture2D texture = BindMesh.ReadPixelsFromFile(colorPath);

            // Robe vertices below the hem top that carry leg weight, plus any leg-weighted vertex in the
            // belt band (the belt sits over the robe top; leaving it on the thighs tears its lower edge).
            var robe = new List<int>();
            var belt = new HashSet<int>();
            float hemBottom = float.MaxValue;
            for (int i = 0; i < mesh.positions.Length; i++)
            {
                float h = mesh.HeightFraction(mesh.positions[i]);
                if (LegWeight(mesh.weights[i], leg) <= 0f)
                    continue;
                if (h >= config.beltBand.x && h <= config.beltBand.y)
                {
                    robe.Add(i);
                    belt.Add(i);
                    continue;
                }
                if (h > config.hemTop)
                    continue;
                Color c = texture.GetPixelBilinear(mesh.uv[i].x, mesh.uv[i].y);
                if (PipelineMath.Luminance(c) < config.hemRobeMinLuminance)
                    continue;
                robe.Add(i);
                hemBottom = Mathf.Min(hemBottom, h);
            }
            Object.DestroyImmediate(texture);
            if (!config.moveHemWeights)
            {
                robe.Clear();
                belt.Clear();
            }

            BoneWeight[] weights = mesh.weights.ToArray();
            int welded = config.weldSeamWeights ? WeldSeams(mesh.source.vertices, weights) : 0;
            float movedTotal = 0f;
            foreach (int i in robe)
            {
                float share = belt.Contains(i) ? 1f : PipelineMath.HemHipShare(mesh.HeightFraction(mesh.positions[i]), hemBottom, config.hemTop, config.hemBottomHipShare);
                weights[i] = MoveToBone(weights[i], leg, hips, share, out float moved);
                movedTotal += moved;
            }

            // Split seam vertices may have been classified differently (each has its own UV); weld again.
            if (config.weldSeamWeights)
                WeldSeams(mesh.source.vertices, weights);

            string path = MeshPath(config);
            var copy = Object.Instantiate(mesh.source);
            copy.name = config.outputName + "_Mesh";
            copy.boneWeights = weights;
            var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(copy, existing);
                Object.DestroyImmediate(copy);
            }
            else
            {
                AssetDatabase.CreateAsset(copy, path);
            }
            AssetDatabase.SaveAssets();
            return $"Seam weld: {welded} seam positions had differing weights (averaged)\n"
                   + $"Hem weights: {robe.Count - belt.Count} robe vertices between h {hemBottom:F3} and {config.hemTop:F3} and {belt.Count} belt vertices -> hips '{hipsName}' "
                   + $"(share {config.hemBottomHipShare:F2} at the bottom, 1 at the top), mean weight moved {(robe.Count > 0 ? movedTotal / robe.Count : 0f):F2}; saved {path}\n";
        }

        // The auto-rig gives the duplicated vertices along UV seams independent weights, so the mesh
        // cracks open wherever it bends. Vertices at the same position get the mean of their weights
        // (four largest kept). Returns how many seam positions had differing weights.
        public static int WeldSeams(Vector3[] positions, BoneWeight[] weights)
        {
            var groups = new Dictionary<Vector3Int, List<int>>();
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 p = positions[i];
                var key = new Vector3Int(Mathf.RoundToInt(p.x * 1e5f), Mathf.RoundToInt(p.y * 1e5f), Mathf.RoundToInt(p.z * 1e5f));
                if (!groups.TryGetValue(key, out List<int> list))
                    groups[key] = list = new List<int>();
                list.Add(i);
            }
            int differing = 0;
            foreach (List<int> group in groups.Values)
            {
                if (group.Count < 2)
                    continue;
                var sum = new Dictionary<int, float>();
                foreach (int i in group)
                {
                    BoneWeight w = weights[i];
                    Accumulate(sum, w.boneIndex0, w.weight0);
                    Accumulate(sum, w.boneIndex1, w.weight1);
                    Accumulate(sum, w.boneIndex2, w.weight2);
                    Accumulate(sum, w.boneIndex3, w.weight3);
                }
                BoneWeight mean = FromMap(sum);
                bool differs = false;
                foreach (int i in group)
                {
                    if (!SameWeights(weights[i], mean))
                        differs = true;
                    weights[i] = mean;
                }
                if (differs)
                    differing++;
            }
            return differing;
        }

        static void Accumulate(Dictionary<int, float> map, int bone, float weight)
        {
            if (weight <= 0f)
                return;
            map.TryGetValue(bone, out float v);
            map[bone] = v + weight;
        }

        static BoneWeight FromMap(Dictionary<int, float> map)
        {
            var top = map.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).Take(4).ToList();
            float total = top.Sum(kv => kv.Value);
            var r = new BoneWeight();
            for (int k = 0; k < top.Count; k++)
            {
                float v = total > 0f ? top[k].Value / total : 0f;
                switch (k)
                {
                    case 0: r.boneIndex0 = top[k].Key; r.weight0 = v; break;
                    case 1: r.boneIndex1 = top[k].Key; r.weight1 = v; break;
                    case 2: r.boneIndex2 = top[k].Key; r.weight2 = v; break;
                    case 3: r.boneIndex3 = top[k].Key; r.weight3 = v; break;
                }
            }
            return r;
        }

        static bool SameWeights(BoneWeight a, BoneWeight b)
        {
            var map = new Dictionary<int, float>();
            Accumulate(map, a.boneIndex0, a.weight0);
            Accumulate(map, a.boneIndex1, a.weight1);
            Accumulate(map, a.boneIndex2, a.weight2);
            Accumulate(map, a.boneIndex3, a.weight3);
            float diff = 0f;
            int[] bi = { b.boneIndex0, b.boneIndex1, b.boneIndex2, b.boneIndex3 };
            float[] bw = { b.weight0, b.weight1, b.weight2, b.weight3 };
            for (int k = 0; k < 4; k++)
            {
                if (bw[k] <= 0f)
                    continue;
                map.TryGetValue(bi[k], out float v);
                map[bi[k]] = v - bw[k];
            }
            foreach (float v in map.Values)
                diff += Mathf.Abs(v);
            return diff < 0.01f;
        }

        static float LegWeight(BoneWeight w, HashSet<int> leg)
        {
            float s = 0f;
            if (leg.Contains(w.boneIndex0)) s += w.weight0;
            if (leg.Contains(w.boneIndex1)) s += w.weight1;
            if (leg.Contains(w.boneIndex2)) s += w.weight2;
            if (leg.Contains(w.boneIndex3)) s += w.weight3;
            return s;
        }

        // Moves `share` of the leg weight to `target`, keeps the four largest influences, renormalises.
        public static BoneWeight MoveToBone(BoneWeight w, HashSet<int> leg, int target, float share, out float moved)
        {
            var map = new Dictionary<int, float>();
            void Add(int bone, float weight)
            {
                if (weight <= 0f)
                    return;
                map.TryGetValue(bone, out float v);
                map[bone] = v + weight;
            }
            moved = 0f;
            int[] idx = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
            float[] wt = { w.weight0, w.weight1, w.weight2, w.weight3 };
            for (int k = 0; k < 4; k++)
            {
                if (wt[k] <= 0f)
                    continue;
                if (leg.Contains(idx[k]))
                {
                    float m = wt[k] * share;
                    moved += m;
                    Add(idx[k], wt[k] - m);
                    Add(target, m);
                }
                else
                {
                    Add(idx[k], wt[k]);
                }
            }
            var top = map.OrderByDescending(kv => kv.Value).Take(4).ToList();
            float sum = top.Sum(kv => kv.Value);
            var r = new BoneWeight();
            for (int k = 0; k < top.Count; k++)
            {
                float v = sum > 0f ? top[k].Value / sum : 0f;
                switch (k)
                {
                    case 0: r.boneIndex0 = top[k].Key; r.weight0 = v; break;
                    case 1: r.boneIndex1 = top[k].Key; r.weight1 = v; break;
                    case 2: r.boneIndex2 = top[k].Key; r.weight2 = v; break;
                    case 3: r.boneIndex3 = top[k].Key; r.weight3 = v; break;
                }
            }
            return r;
        }
    }
}
