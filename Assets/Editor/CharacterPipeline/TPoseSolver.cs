using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CharacterPipeline
{
    // Puts a Humanoid skeleton into an exact T-pose (what the Avatar inspector's "Enforce T-Pose"
    // does): pelvis level, spine and neck vertical, arms horizontal along ±X, legs straight down.
    // The character faces +Z, so its right arm points +X.
    // Works on any transforms looked up by Humanoid bone name, so it is testable without an FBX.
    public static class TPoseSolver
    {
        public struct Limb
        {
            public string label;
            public string from;
            public string to;
            public Vector3 target;

            public Limb(string label, string from, string to, Vector3 target)
            {
                this.label = label;
                this.from = from;
                this.to = to;
                this.target = target;
            }
        }

        // Order matters: parents first, so each step measures the already-corrected chain.
        public static readonly Limb[] Limbs =
        {
            new Limb("Spine", "Spine", "Chest", Vector3.up),
            new Limb("Chest", "Chest", "UpperChest", Vector3.up),
            new Limb("UpperChest", "UpperChest", "Neck", Vector3.up),
            new Limb("Neck", "Neck", "Head", Vector3.up),
            new Limb("L Shoulder", "LeftShoulder", "LeftUpperArm", Vector3.left),
            new Limb("L UpperArm", "LeftUpperArm", "LeftLowerArm", Vector3.left),
            new Limb("L LowerArm", "LeftLowerArm", "LeftHand", Vector3.left),
            new Limb("R Shoulder", "RightShoulder", "RightUpperArm", Vector3.right),
            new Limb("R UpperArm", "RightUpperArm", "RightLowerArm", Vector3.right),
            new Limb("R LowerArm", "RightLowerArm", "RightHand", Vector3.right),
            new Limb("L UpperLeg", "LeftUpperLeg", "LeftLowerLeg", Vector3.down),
            new Limb("L LowerLeg", "LeftLowerLeg", "LeftFoot", Vector3.down),
            new Limb("R UpperLeg", "RightUpperLeg", "RightLowerLeg", Vector3.down),
            new Limb("R LowerLeg", "RightLowerLeg", "RightFoot", Vector3.down),
        };

        // Angle of each limb from its T-pose direction (degrees), plus the pelvis tilt.
        public static Dictionary<string, float> Measure(Func<string, Transform> bone)
        {
            var result = new Dictionary<string, float>();
            Transform l = bone("LeftUpperLeg"), r = bone("RightUpperLeg");
            if (l != null && r != null)
                result["Pelvis"] = Vector3.Angle(r.position - l.position, Vector3.right);
            foreach (Limb limb in Limbs)
            {
                Transform a = bone(limb.from), b = bone(limb.to);
                // Skip optional bones (e.g. no UpperChest): the chain then measures to the next one.
                if (a == null || b == null)
                    continue;
                result[limb.label] = PipelineMath.AngleOff(a, b, limb.target);
            }
            return result;
        }

        // Rotates the bones into the T-pose. Optional spine bones that are missing are bridged
        // (Spine -> Neck when there is no Chest/UpperChest).
        public static void Solve(Func<string, Transform> bone)
        {
            Transform l = bone("LeftUpperLeg"), r = bone("RightUpperLeg"), hips = bone("Hips");
            if (hips != null && l != null && r != null)
            {
                Vector3 pelvis = r.position - l.position;
                hips.rotation = Quaternion.FromToRotation(pelvis, Vector3.right) * hips.rotation;
            }

            var spine = new List<Transform>();
            foreach (string name in new[] { "Spine", "Chest", "UpperChest", "Neck", "Head" })
            {
                Transform t = bone(name);
                if (t != null)
                    spine.Add(t);
            }
            for (int i = 0; i + 1 < spine.Count; i++)
                PipelineMath.AlignTowards(spine[i], spine[i + 1], Vector3.up);

            foreach (Limb limb in Limbs)
            {
                if (limb.target == Vector3.up)
                    continue;
                PipelineMath.AlignTowards(bone(limb.from), bone(limb.to), limb.target);
            }
        }

        public static string Format(Dictionary<string, float> before, Dictionary<string, float> after)
        {
            var sb = new StringBuilder();
            foreach (KeyValuePair<string, float> kv in before)
            {
                float a = after != null && after.TryGetValue(kv.Key, out float v) ? v : float.NaN;
                sb.Append(kv.Key).Append(' ').Append(kv.Value.ToString("F1")).Append("° -> ").Append(a.ToString("F2")).Append("°\n");
            }
            return sb.ToString();
        }
    }
}
