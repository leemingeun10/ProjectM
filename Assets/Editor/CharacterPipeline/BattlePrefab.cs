using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CharacterPipeline
{
    // Steps 4, 5, 8, 9: builds <outputName>_Fist.prefab as a variant of the rigged model prefab:
    // bones in the corrected T-pose, Animator + avatar + controller, scaled to the target height,
    // origin at the soles, bounds that follow the hips, the matte material, the hem-weight mesh,
    // and a SwordSocket under the right hand at the measured fist hole.
    public static class BattlePrefab
    {
        public const string SocketName = "SwordSocket";

        public static string PrefabPath(CharacterPipelineConfig config) => MaterialStandard.OutputFolder(config) + "/" + config.outputName + "_Fist.prefab";

        public static string Build(CharacterPipelineConfig config)
        {
            string fbx = HumanoidSetup.ModelPath(config);
            Avatar avatar = HumanoidSetup.LoadAvatar(fbx);
            HumanBone[] human = avatar.humanDescription.human;
            string Bone(string humanName) => human.First(h => h.humanName == humanName).boneName;
            Dictionary<string, Quaternion> pose = HumanoidSetup.StoredPose(config);
            var report = new StringBuilder("Battle prefab:\n");

            // A preview scene keeps the open scene untouched (no dirty flag, nothing left behind).
            UnityEngine.SceneManagement.Scene preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(config.modelPrefab, preview);
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.transform.localScale = Vector3.one;
                Dictionary<string, Transform> byName = instance.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
                int posed = 0;
                foreach (KeyValuePair<string, Quaternion> kv in pose)
                {
                    if (byName.TryGetValue(kv.Key, out Transform t) && t != instance.transform)
                    {
                        t.localRotation = kv.Value;
                        posed++;
                    }
                }

                var smr = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var hemMesh = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(HemWeights.MeshPath(config));
                if (hemMesh != null)
                    smr.sharedMesh = hemMesh;
                var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialStandard.MaterialPath(config));
                if (material != null)
                    smr.sharedMaterial = material;

                var animator = instance.GetComponent<Animator>();
                if (animator == null)
                    animator = instance.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = config.animatorController;
                animator.applyRootMotion = false;

                // Height and soles (measured on the T-posed mesh).
                MeasureMesh(smr, out float minY, out float maxY);
                config.measuredHeightBeforeScale = maxY - minY;
                float scale = PipelineMath.ScaleForHeight(maxY - minY, config.targetHeight);
                config.appliedScale = scale;
                instance.transform.localScale = Vector3.one * scale;
                MeasureMesh(smr, out minY, out maxY);
                foreach (Transform child in instance.transform)
                    child.localPosition += Vector3.up * (-minY / scale);
                MeasureMesh(smr, out minY, out maxY);
                report.Append($" bones re-posed {posed}; mesh height {config.measuredHeightBeforeScale:F3} -> scale {scale:F4} -> {maxY - minY:F3} m, soles at y {minY:F4}\n");

                Transform hips = byName[Bone("Hips")];
                smr.rootBone = hips;
                float half = 1.3f / hips.lossyScale.x;
                smr.localBounds = new Bounds(Vector3.zero, Vector3.one * half * 2f);
                report.Append($" rootBone {hips.name}, bounds ±1.3 m around the hips\n");

                // Sword socket at the fist hole, blade along the body's forward axis.
                Transform hand = byName[Bone("RightHand")];
                Transform old = hand.Find(SocketName);
                if (old != null)
                    Object.DestroyImmediate(old.gameObject);
                SwordSocket.Result hole = SwordSocket.MeasureFistHole(smr, hand, byName[Bone("RightLowerArm")], instance.transform.forward, config.socketGridStep);
                report.Append(hole.report);
                if (hole.found)
                {
                    var socket = new GameObject(SocketName).transform;
                    socket.SetParent(hand, false);
                    socket.SetPositionAndRotation(hole.centre, Quaternion.LookRotation(instance.transform.forward, instance.transform.up));
                    config.socketLocalPosition = socket.localPosition;
                    config.socketLocalRotation = socket.localRotation;
                    config.fistHoleThrough = hole.through;
                }

                string path = PrefabPath(config);
                PrefabUtility.SaveAsPrefabAsset(instance, path);
                report.Append($" saved {path}\n");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);
            }

            report.Append(MeasureIdleOffset(config));
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return report.ToString();
        }

        internal static void MeasureMesh(SkinnedMeshRenderer smr, out float minY, out float maxY)
        {
            var baked = new UnityEngine.Mesh();
            smr.BakeMesh(baked, false);
            Matrix4x4 m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            minY = float.MaxValue;
            maxY = float.MinValue;
            foreach (Vector3 v in baked.vertices)
            {
                float y = m.MultiplyPoint3x4(v).y;
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }
            Object.DestroyImmediate(baked);
        }

        // Lowest sole of each foot (vertices dominated by the foot subtree) in the current pose.
        internal static void SoleHeights(SkinnedMeshRenderer smr, Animator animator, out float left, out float right)
        {
            var leftSet = new HashSet<Transform>(animator.GetBoneTransform(HumanBodyBones.LeftFoot).GetComponentsInChildren<Transform>(true));
            var rightSet = new HashSet<Transform>(animator.GetBoneTransform(HumanBodyBones.RightFoot).GetComponentsInChildren<Transform>(true));
            var baked = new UnityEngine.Mesh();
            smr.BakeMesh(baked, false);
            Matrix4x4 m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            Vector3[] verts = baked.vertices;
            BoneWeight[] bw = smr.sharedMesh.boneWeights;
            Transform[] bones = smr.bones;
            left = float.MaxValue;
            right = float.MaxValue;
            for (int i = 0; i < verts.Length; i++)
            {
                Transform b = bones[bw[i].boneIndex0];
                if (leftSet.Contains(b))
                    left = Mathf.Min(left, m.MultiplyPoint3x4(verts[i]).y);
                else if (rightSet.Contains(b))
                    right = Mathf.Min(right, m.MultiplyPoint3x4(verts[i]).y);
            }
            Object.DestroyImmediate(baked);
        }

        // Plays Idle on the saved prefab and finds the offset that puts the lower sole on the floor.
        public static string MeasureIdleOffset(CharacterPipelineConfig config)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(config));
            var instance = Object.Instantiate(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var animator = instance.GetComponent<Animator>();
                var smr = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
                float left = float.MaxValue, right = float.MaxValue;
                animator.Rebind();
                for (int k = 0; k < 8; k++)
                {
                    animator.Play("Idle", 0, k / 8f);
                    animator.Update(0f);
                    SoleHeights(smr, animator, out float l, out float r);
                    left = Mathf.Min(left, l);
                    right = Mathf.Min(right, r);
                }
                config.idleHeightOffset = PipelineMath.GroundOffset(left, right);
                return $" Idle soles (lowest over 8 samples): left {left * 100f:F1} cm, right {right * 100f:F1} cm -> height offset {config.idleHeightOffset * 100f:F1} cm "
                       + $"(after it: left {(left + config.idleHeightOffset) * 100f:F1}, right {(right + config.idleHeightOffset) * 100f:F1} cm)\n";
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
