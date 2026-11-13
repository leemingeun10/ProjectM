using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CharacterPipeline
{
    // Step 10: review report and captures for a built battle prefab. Everything runs in a preview
    // scene (the open scene is not touched). Captures are written to `captureDir`.
    public static class CharacterInspector
    {
        public static readonly string[] CombatStates = { "Combat_Thrust", "Combat_SlashLR", "Combat_SlashRL", "Combat_SlashDown", "Combat_Counter" };
        public static readonly string[] HemStates = { "Combat_SlashLR", "Combat_SlashDown", "Combat_Thrust" };
        const float StrikeSeconds = 0.48f;
        const string ReferenceModel = "Assets/CharacterArt/Character_TPose.fbx";

        sealed class Stage : IDisposable
        {
            public UnityEngine.SceneManagement.Scene scene;
            public Camera camera;
            public Light light;
            public RenderTexture target;
            readonly List<Object> temporary = new List<Object>();

            public Stage()
            {
                scene = EditorSceneManager.NewPreviewScene();
                var camGo = new GameObject("InspectorCamera");
                SceneManagerMove(camGo);
                camera = camGo.AddComponent<Camera>();
                camera.scene = scene;
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.42f, 0.42f, 0.44f);
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 30f;
                var lightGo = new GameObject("InspectorLight");
                SceneManagerMove(lightGo);
                light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                target = new RenderTexture(1024, 1024, 24);
                camera.targetTexture = target;
            }

            public void SceneManagerMove(GameObject go)
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            }

            public void Track(Object o) => temporary.Add(o);

            public void Shot(string path, Vector3 lookDir, Vector3 centre, float halfSize)
            {
                camera.orthographicSize = halfSize;
                camera.transform.SetPositionAndRotation(centre - lookDir.normalized * 10f, Quaternion.LookRotation(lookDir, Vector3.up));
                light.transform.rotation = Quaternion.LookRotation(lookDir.normalized + Vector3.down * 0.6f + Vector3.right * 0.2f, Vector3.up);
                camera.Render();
                RenderTexture.active = target;
                var t = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                t.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                t.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(path, t.EncodeToPNG());
                Object.DestroyImmediate(t);
            }

            public void Dispose()
            {
                camera.targetTexture = null;
                target.Release();
                foreach (Object o in temporary)
                {
                    if (o != null)
                        Object.DestroyImmediate(o);
                }
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // Shows the skinned mesh as a baked static copy (edit-mode renders of skinned meshes are not reliable).
        sealed class BakedView : IDisposable
        {
            readonly SkinnedMeshRenderer smr;
            readonly GameObject go;
            readonly UnityEngine.Mesh mesh;
            readonly MeshRenderer renderer;

            public BakedView(Stage stage, SkinnedMeshRenderer smr)
            {
                this.smr = smr;
                go = new GameObject("Baked");
                stage.SceneManagerMove(go);
                mesh = new UnityEngine.Mesh();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderer = go.AddComponent<MeshRenderer>();
                smr.enabled = false;
            }

            public void Refresh(Material material = null)
            {
                smr.BakeMesh(mesh, false);
                go.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
                renderer.sharedMaterials = material != null ? new[] { material } : smr.sharedMaterials;
            }

            public void Dispose()
            {
                smr.enabled = true;
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(mesh);
            }
        }

        public static string Run(CharacterPipelineConfig config, string captureDir)
        {
            Directory.CreateDirectory(captureDir);
            string prefix = Path.Combine(captureDir, config.outputName + "_");
            var sb = new StringBuilder("Inspection:\n");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattlePrefab.PrefabPath(config));
            using (var stage = new Stage())
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, stage.scene);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var animator = instance.GetComponent<Animator>();
                var smr = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Func<string, Transform> bone = n => Enum.TryParse(n, out HumanBodyBones b) ? animator.GetBoneTransform(b) : null;

                // Bind (corrected T-pose) numbers, before the Animator touches the bones.
                BattlePrefab.MeasureMesh(smr, out float minY, out float maxY);
                Dictionary<string, float> angles = TPoseSolver.Measure(bone);
                sb.Append($" triangles {smr.sharedMesh.triangles.Length / 3}, vertices {smr.sharedMesh.vertexCount}, materials {smr.sharedMaterials.Length}, bones {smr.bones.Length}\n");
                sb.Append($" height {maxY - minY:F3} m (scale {config.appliedScale:F4}), soles at {minY:F4}\n");
                sb.Append(" bind T-pose angle from target: " + string.Join(", ", angles.Select(kv => kv.Key + " " + kv.Value.ToString("F2") + "°")) + "\n");
                sb.Append(Proportions(stage, animator, smr, minY, maxY));

                Vector3[] bind = Bake(smr);
                UnityEngine.Mesh hemMesh = smr.sharedMesh;
                // "Before" for the hem comparison = generated weights with the seams welded only, so
                // seam cracks don't muddy the comparison.
                UnityEngine.Mesh generated = config.modelPrefab.GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMesh;
                UnityEngine.Mesh original = Object.Instantiate(generated);
                BoneWeight[] weldedOnly = generated.boneWeights;
                if (config.weldSeamWeights)
                    HemWeights.WeldSeams(generated.vertices, weldedOnly);
                original.boneWeights = weldedOnly;
                stage.Track(original);
                int hipsIndex = Array.IndexOf(smr.bones, animator.GetBoneTransform(HumanBodyBones.Hips));
                List<int2> hemEdges = HemEdges(original, hemMesh, hipsIndex);
                sb.Append($" hem edges compared: {hemEdges.Count}\n");

                // Sword cube on the socket, like the battle TempSword (0.05 x 0.05 x 0.8 m, centred forward).
                Transform socket = bone("RightHand") != null ? bone("RightHand").Find(BattlePrefab.SocketName) : null;
                GameObject sword = null;
                if (socket != null)
                {
                    sword = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.DestroyImmediate(sword.GetComponent<Collider>());
                    sword.name = "InspectorSword";
                    sword.transform.SetParent(socket, false);
                    float s = socket.lossyScale.x;
                    sword.transform.localPosition = new Vector3(0f, 0f, config.swordForwardOffset / s);
                    sword.transform.localScale = new Vector3(0.05f, 0.05f, 0.8f) / s;
                    var swordMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    swordMat.color = new Color(0.75f, 0.75f, 0.78f);
                    swordMat.SetFloat("_Smoothness", 0.2f);
                    stage.Track(swordMat);
                    sword.GetComponent<MeshRenderer>().sharedMaterial = swordMat;
                }
                else
                {
                    sb.Append(" no SwordSocket found\n");
                }

                float H = maxY - minY;
                Vector3 bodyCentre = new Vector3(0f, H * 0.5f, 0f);
                using (var view = new BakedView(stage, smr))
                {
                    view.Refresh();
                    stage.Shot(prefix + "bind_front.png", Vector3.back, bodyCentre, H * 0.58f);
                    stage.Shot(prefix + "bind_side.png", Vector3.left, bodyCentre, H * 0.58f);
                    stage.Shot(prefix + "bind_back.png", Vector3.forward, bodyCentre, H * 0.58f);

                    // Face: smoothness 0.5 vs the standard value vs plain albedo.
                    Vector3 head = bone("Head").position + Vector3.up * 0.06f;
                    Material std = smr.sharedMaterial;
                    var glossy = new Material(std);
                    glossy.SetFloat("_Smoothness", 0.5f);
                    stage.Track(glossy);
                    var unlit = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    unlit.SetTexture("_BaseMap", std.GetTexture("_BaseMap"));
                    stage.Track(unlit);
                    view.Refresh(glossy);
                    stage.Shot(prefix + "face_smooth050.png", Vector3.back, head, 0.13f);
                    view.Refresh(std);
                    stage.Shot(prefix + "face_smooth" + Mathf.RoundToInt(config.smoothness * 100).ToString("000") + ".png", Vector3.back, head, 0.13f);
                    view.Refresh(unlit);
                    stage.Shot(prefix + "face_albedo.png", Vector3.back, head, 0.13f);
                    view.Refresh();

                    animator.Rebind();
                    sb.Append(PlayIdle(stage, view, animator, smr, config, prefix, bodyCentre, H, bone));
                    foreach (string state in CombatStates)
                        sb.Append(PlayCombat(stage, view, animator, smr, config, prefix, state, bodyCentre, H, bone, bind, original, hemMesh, hemEdges));
                }
                if (sword != null)
                    Object.DestroyImmediate(sword);
            }
            sb.Append($" captures: {captureDir}\n");
            return sb.ToString();
        }

        static Vector3 FistCentre(Func<string, Transform> bone)
        {
            Transform hand = bone("RightHand");
            Transform socket = hand.Find(BattlePrefab.SocketName);
            return socket != null ? socket.position : hand.position;
        }

        struct int2
        {
            public int a, b;
            public int2(int a, int b) { this.a = a; this.b = b; }
        }

        static float WeightOf(BoneWeight w, int bone)
        {
            float s = 0f;
            if (w.boneIndex0 == bone) s += w.weight0;
            if (w.boneIndex1 == bone) s += w.weight1;
            if (w.boneIndex2 == bone) s += w.weight2;
            if (w.boneIndex3 == bone) s += w.weight3;
            return s;
        }

        // Edges touching vertices whose hips weight the hem step raised (seam welding alone doesn't count).
        static List<int2> HemEdges(UnityEngine.Mesh original, UnityEngine.Mesh hem, int hipsIndex)
        {
            var edges = new List<int2>();
            if (original == hem || hipsIndex < 0)
                return edges;
            BoneWeight[] a = original.boneWeights, b = hem.boneWeights;
            var changed = new bool[a.Length];
            for (int i = 0; i < a.Length; i++)
                changed[i] = WeightOf(b[i], hipsIndex) - WeightOf(a[i], hipsIndex) > 0.2f;
            int[] tris = hem.triangles;
            var seen = new HashSet<long>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                for (int k = 0; k < 3; k++)
                {
                    int i = tris[t + k], j = tris[t + (k + 1) % 3];
                    if (!changed[i] && !changed[j])
                        continue;
                    long key = (long)Mathf.Min(i, j) << 32 | (uint)Mathf.Max(i, j);
                    if (seen.Add(key))
                        edges.Add(new int2(i, j));
                }
            }
            return edges;
        }

        static Vector3[] Bake(SkinnedMeshRenderer smr)
        {
            var m = new UnityEngine.Mesh();
            smr.BakeMesh(m, false);
            Matrix4x4 toWorld = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            Vector3[] v = m.vertices;
            for (int i = 0; i < v.Length; i++)
                v[i] = toWorld.MultiplyPoint3x4(v[i]);
            Object.DestroyImmediate(m);
            return v;
        }

        // 95th percentile of edge length / bind length (single sliver edges dominate a plain max).
        static float MaxStretch(Vector3[] bind, Vector3[] posed, List<int2> edges)
        {
            var ratios = new List<float>(edges.Count);
            foreach (int2 e in edges)
            {
                float b = (bind[e.a] - bind[e.b]).magnitude;
                if (b < 1e-4f)
                    continue;
                ratios.Add((posed[e.a] - posed[e.b]).magnitude / b);
            }
            if (ratios.Count == 0)
                return 1f;
            ratios.Sort();
            return ratios[Mathf.Min(ratios.Count - 1, (int)(ratios.Count * 0.95f))];
        }

        static string PlayIdle(Stage stage, BakedView view, Animator animator, SkinnedMeshRenderer smr, CharacterPipelineConfig config, string prefix, Vector3 bodyCentre, float H, Func<string, Transform> bone)
        {
            animator.Play("Idle", 0, 0f);
            animator.Update(0f);
            BattlePrefab.SoleHeights(smr, animator, out float l, out float r);
            float off = config.idleHeightOffset;
            view.Refresh();
            stage.Shot(prefix + "idle_front.png", Vector3.back, bodyCentre, H * 0.58f);
            stage.Shot(prefix + "idle_side.png", Vector3.left, bodyCentre, H * 0.58f);
            Vector3 fist = FistCentre(bone);
            stage.Shot(prefix + "fist_sword_idle.png", new Vector3(-0.6f, -0.2f, -1f), fist + Vector3.forward * 0.15f, 0.22f);
            stage.Shot(prefix + "fist_sword_idle_close.png", new Vector3(-0.3f, -0.3f, -1f), fist, 0.09f);
            stage.Shot(prefix + "fist_sword_idle_close_top.png", new Vector3(0f, -1f, 0.15f), fist, 0.09f);
            return $" Idle t=0: soles left {(l + off) * 100f:F1} cm, right {(r + off) * 100f:F1} cm (with offset {off * 100f:F1} cm)\n";
        }

        static string PlayCombat(Stage stage, BakedView view, Animator animator, SkinnedMeshRenderer smr, CharacterPipelineConfig config, string prefix, string state,
            Vector3 bodyCentre, float H, Func<string, Transform> bone, Vector3[] bind, UnityEngine.Mesh original, UnityEngine.Mesh hemMesh, List<int2> hemEdges)
        {
            AnimationClip clip = animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c.name == state);
            if (clip == null)
                return $" {state}: clip not found on the controller\n";
            float off = config.idleHeightOffset;
            const int samples = 21;
            float minL = float.MaxValue, minR = float.MaxValue, maxL = float.MinValue, maxR = float.MinValue, stretchAfter = 1f, stretchBefore = 1f, spreadBest = -1f, spreadTime = 0f;
            bool nan = false;
            for (int k = 0; k < samples; k++)
            {
                float t = k / (float)(samples - 1);
                animator.Play(state, 0, t);
                animator.Update(0f);
                BattlePrefab.SoleHeights(smr, animator, out float l, out float r);
                minL = Mathf.Min(minL, l); minR = Mathf.Min(minR, r); maxL = Mathf.Max(maxL, l); maxR = Mathf.Max(maxR, r);
                Vector3 hips = bone("Hips").position;
                if (float.IsNaN(hips.x) || float.IsNaN(hips.y))
                    nan = true;
                float spread = (bone("LeftFoot").position - bone("RightFoot").position).magnitude;
                if (spread > spreadBest)
                {
                    spreadBest = spread;
                    spreadTime = t;
                }
                if (hemEdges.Count > 0)
                {
                    stretchAfter = Mathf.Max(stretchAfter, MaxStretch(bind, Bake(smr), hemEdges));
                    smr.sharedMesh = original;
                    stretchBefore = Mathf.Max(stretchBefore, MaxStretch(bind, Bake(smr), hemEdges));
                    smr.sharedMesh = hemMesh;
                }
            }

            // Strike frame: body front and fist + sword close-up.
            float strikeNorm = Mathf.Clamp01(StrikeSeconds / clip.length);
            animator.Play(state, 0, strikeNorm);
            animator.Update(0f);
            view.Refresh();
            stage.Shot(prefix + state + "_strike_front.png", Vector3.back, bodyCentre, H * 0.62f);
            stage.Shot(prefix + state + "_strike_side.png", Vector3.left, bodyCentre, H * 0.62f);
            stage.Shot(prefix + state + "_strike_fist.png", new Vector3(-0.6f, -0.2f, -1f), FistCentre(bone), 0.12f);

            var sb = new StringBuilder();
            sb.Append($" {state} ({clip.length:F2} s): plays {(nan ? "NO (NaN pose)" : "yes")}; soles left {(minL + off) * 100f:F1}..{(maxL + off) * 100f:F1} cm, right {(minR + off) * 100f:F1}..{(maxR + off) * 100f:F1} cm");
            if (hemEdges.Count > 0)
                sb.Append($"; hem edge stretch (p95, worst frame) {stretchBefore:F2}x before -> {stretchAfter:F2}x after");
            sb.Append('\n');

            if (hemEdges.Count > 0 && HemStates.Contains(state))
            {
                animator.Play(state, 0, spreadTime);
                animator.Update(0f);
                Vector3 hip = bone("Hips").position;
                smr.sharedMesh = original;
                view.Refresh();
                stage.Shot(prefix + state + "_hem_before_front.png", Vector3.back, hip, 0.42f);
                stage.Shot(prefix + state + "_hem_before_below.png", new Vector3(0f, 1f, 0.35f), hip, 0.42f);
                smr.sharedMesh = hemMesh;
                view.Refresh();
                stage.Shot(prefix + state + "_hem_after_front.png", Vector3.back, hip, 0.42f);
                stage.Shot(prefix + state + "_hem_after_below.png", new Vector3(0f, 1f, 0.35f), hip, 0.42f);
                sb.Append($"   widest stance at t={spreadTime:F2} (feet {spreadBest:F2} m apart): hem captures saved\n");
            }
            return sb.ToString();
        }

        // Proportions next to the player skeleton (Character_TPose), as fractions of mesh height.
        static string Proportions(Stage stage, Animator animator, SkinnedMeshRenderer smr, float minY, float maxY)
        {
            string Line(string label, Animator a, float soles, float height)
            {
                Vector3 P(HumanBodyBones b) => a.GetBoneTransform(b).position;
                float hip = (P(HumanBodyBones.LeftUpperLeg).y + P(HumanBodyBones.RightUpperLeg).y) * 0.5f - soles;
                float shoulders = (P(HumanBodyBones.LeftUpperArm) - P(HumanBodyBones.RightUpperArm)).magnitude;
                float arm = (P(HumanBodyBones.RightUpperArm) - P(HumanBodyBones.RightLowerArm)).magnitude + (P(HumanBodyBones.RightLowerArm) - P(HumanBodyBones.RightHand)).magnitude;
                float leg = (P(HumanBodyBones.RightUpperLeg) - P(HumanBodyBones.RightLowerLeg)).magnitude + (P(HumanBodyBones.RightLowerLeg) - P(HumanBodyBones.RightFoot)).magnitude;
                return $"  {label}: height {height:F3} m, hip joint {hip / height:F3}, shoulder width {shoulders / height:F3}, arm {arm / height:F3}, leg {leg / height:F3} (of height)\n";
            }

            var sb = new StringBuilder(" proportions:\n");
            sb.Append(Line("this", animator, minY, maxY - minY));
            var reference = AssetDatabase.LoadAssetAtPath<GameObject>(ReferenceModel);
            if (reference != null)
            {
                var r = (GameObject)PrefabUtility.InstantiatePrefab(reference, stage.scene);
                r.transform.SetPositionAndRotation(new Vector3(5f, 0f, 0f), Quaternion.identity);
                float rMin = float.MaxValue, rMax = float.MinValue;
                foreach (SkinnedMeshRenderer s in r.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    BattlePrefab.MeasureMesh(s, out float a, out float b);
                    rMin = Mathf.Min(rMin, a);
                    rMax = Mathf.Max(rMax, b);
                }
                sb.Append(Line("player (Character_TPose)", r.GetComponent<Animator>(), rMin, rMax - rMin));
                Object.DestroyImmediate(r);
            }
            return sb.ToString();
        }
    }
}
