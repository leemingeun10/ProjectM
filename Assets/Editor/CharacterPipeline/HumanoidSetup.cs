using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CharacterPipeline
{
    // Step 1-2: import the rigged FBX as Humanoid (auto mapping, stored explicitly in the .meta) and
    // rewrite the Avatar's reference skeleton into an exact T-pose.
    public static class HumanoidSetup
    {
        public static string ModelPath(CharacterPipelineConfig config)
        {
            string prefabPath = AssetDatabase.GetAssetPath(config.modelPrefab);
            string fbx = AssetDatabase.GetDependencies(prefabPath, true).FirstOrDefault(d => AssetImporter.GetAtPath(d) is ModelImporter);
            if (fbx == null)
                throw new InvalidOperationException($"No model file found under {prefabPath}.");
            return fbx;
        }

        public static Avatar LoadAvatar(string fbxPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Avatar>().FirstOrDefault();
        }

        public static string ImportHumanoid(CharacterPipelineConfig config)
        {
            string fbx = ModelPath(config);
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
            }

            Avatar avatar = LoadAvatar(fbx);
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                return $"Humanoid import failed for {fbx}: avatar {(avatar == null ? "missing" : $"valid={avatar.isValid} human={avatar.isHuman}")}.";

            // Store the auto-generated mapping in the importer so a re-import cannot remap silently.
            importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
            HumanDescription description = importer.humanDescription;
            if (description.human == null || description.human.Length == 0)
            {
                description = avatar.humanDescription;
                importer.humanDescription = description;
                importer.SaveAndReimport();
                avatar = LoadAvatar(fbx);
            }

            HumanBone[] human = avatar.humanDescription.human;
            int required = HumanTrait.RequiredBoneCount;
            var mappedNames = new HashSet<string>(human.Select(h => h.humanName));
            int requiredMapped = Enumerable.Range(0, HumanTrait.BoneCount).Count(i => HumanTrait.RequiredBone(i) && mappedNames.Contains(HumanTrait.BoneName[i]));
            int fingers = human.Count(h => h.humanName.Contains("Proximal") || h.humanName.Contains("Intermediate") || h.humanName.Contains("Distal"));
            var sb = new StringBuilder();
            sb.Append($"Humanoid: {fbx}\n mapped {human.Length} bones (required {requiredMapped}/{required}, fingers {fingers})\n");
            foreach (HumanBone h in human.OrderBy(h => Array.IndexOf(HumanTrait.BoneName, h.humanName)))
                sb.Append("  ").Append(h.humanName).Append(" <- ").Append(h.boneName).Append('\n');
            return sb.ToString();
        }

        // Builds a temporary instance of the model with its bones set from `skeleton`.
        internal static GameObject InstantiateWithSkeleton(string fbxPath, SkeletonBone[] skeleton, out Dictionary<string, Transform> byName)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var instance = Object.Instantiate(model);
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            byName = new Dictionary<string, Transform>();
            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
                byName[t.name] = t;
            if (skeleton != null)
            {
                foreach (SkeletonBone s in skeleton)
                {
                    if (s.name == skeleton[0].name)
                        continue; // root entry names the asset, not a transform
                    if (byName.TryGetValue(s.name, out Transform t))
                    {
                        t.localPosition = s.position;
                        t.localRotation = s.rotation;
                        t.localScale = s.scale;
                    }
                }
            }
            return instance;
        }

        internal static Func<string, Transform> HumanLookup(HumanBone[] human, Dictionary<string, Transform> byName)
        {
            var map = new Dictionary<string, Transform>();
            foreach (HumanBone h in human)
            {
                if (byName.TryGetValue(h.boneName, out Transform t))
                    map[h.humanName] = t;
            }
            return n => map.TryGetValue(n, out Transform t) ? t : null;
        }

        // Returns the bone local rotations of the corrected pose (by transform name) for re-posing prefabs.
        public static Dictionary<string, Quaternion> EnforceTPose(CharacterPipelineConfig config, out string report)
        {
            string fbx = ModelPath(config);
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
            HumanDescription description = importer.humanDescription;
            if (description.human == null || description.human.Length == 0)
                throw new InvalidOperationException("Run Import Humanoid first: the importer has no stored bone mapping.");

            // "Before" = the reference skeleton currently stored (bind pose on the first run).
            GameObject instance = InstantiateWithSkeleton(fbx, description.skeleton, out Dictionary<string, Transform> byName);
            try
            {
                Func<string, Transform> bone = HumanLookup(description.human, byName);
                Dictionary<string, float> before = TPoseSolver.Measure(bone);
                TPoseSolver.Solve(bone);
                Dictionary<string, float> after = TPoseSolver.Measure(bone);

                SkeletonBone[] skeleton = description.skeleton;
                var pose = new Dictionary<string, Quaternion>();
                for (int i = 0; i < skeleton.Length; i++)
                {
                    if (i == 0 || !byName.TryGetValue(skeleton[i].name, out Transform t))
                        continue;
                    skeleton[i].position = t.localPosition;
                    skeleton[i].rotation = t.localRotation;
                    skeleton[i].scale = t.localScale;
                    pose[t.name] = t.localRotation;
                }
                description.skeleton = skeleton;
                importer.humanDescription = description;
                importer.SaveAndReimport();

                Avatar avatar = LoadAvatar(fbx);
                report = "T-pose (angle from target, before -> after):\n" + TPoseSolver.Format(before, after)
                         + $"avatar valid={avatar != null && avatar.isValid} human={avatar != null && avatar.isHuman}\n";
                return pose;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        // Current corrected pose (from the stored skeleton) without changing anything.
        public static Dictionary<string, Quaternion> StoredPose(CharacterPipelineConfig config)
        {
            string fbx = ModelPath(config);
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
            var pose = new Dictionary<string, Quaternion>();
            SkeletonBone[] skeleton = importer.humanDescription.skeleton;
            for (int i = 1; i < skeleton.Length; i++)
                pose[skeleton[i].name] = skeleton[i].rotation;
            return pose;
        }
    }
}
