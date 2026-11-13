using System.Collections.Generic;
using CharacterPipeline;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class CharacterPipelineTests
    {
        readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in created)
                Object.DestroyImmediate(go);
            created.Clear();
        }

        Transform Node(string name, Transform parent, Vector3 worldPosition)
        {
            var go = new GameObject(name);
            if (parent == null)
                created.Add(go);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPosition;
            return go.transform;
        }

        static Transform Rotated(Transform t, Vector3 axis, float degrees, Transform pivot)
        {
            t.RotateAround(pivot.position, axis, degrees);
            return t;
        }

        // A minimal Humanoid-like rig in a deliberately crooked bind pose: legs spread 5° outward,
        // shins bent, arms drooping 9°, spine leaning 4°.
        Dictionary<string, Transform> CrookedRig()
        {
            var b = new Dictionary<string, Transform>();
            b["Hips"] = Node("Hips", null, new Vector3(0f, 1f, 0f));
            b["LeftUpperLeg"] = Node("LUpLeg", b["Hips"], new Vector3(-0.1f, 0.95f, 0f));
            b["LeftLowerLeg"] = Node("LLoLeg", b["LeftUpperLeg"], new Vector3(-0.1f, 0.5f, 0f));
            b["LeftFoot"] = Node("LFoot", b["LeftLowerLeg"], new Vector3(-0.1f, 0.08f, 0f));
            b["RightUpperLeg"] = Node("RUpLeg", b["Hips"], new Vector3(0.1f, 0.95f, 0f));
            b["RightLowerLeg"] = Node("RLoLeg", b["RightUpperLeg"], new Vector3(0.1f, 0.5f, 0f));
            b["RightFoot"] = Node("RFoot", b["RightLowerLeg"], new Vector3(0.1f, 0.08f, 0f));
            b["Spine"] = Node("Spine", b["Hips"], new Vector3(0f, 1.1f, 0f));
            b["Chest"] = Node("Chest", b["Spine"], new Vector3(0f, 1.3f, 0f));
            b["Neck"] = Node("Neck", b["Chest"], new Vector3(0f, 1.5f, 0f));
            b["Head"] = Node("Head", b["Neck"], new Vector3(0f, 1.6f, 0f));
            b["LeftShoulder"] = Node("LSh", b["Chest"], new Vector3(-0.05f, 1.45f, 0f));
            b["LeftUpperArm"] = Node("LUpArm", b["LeftShoulder"], new Vector3(-0.18f, 1.45f, 0f));
            b["LeftLowerArm"] = Node("LLoArm", b["LeftUpperArm"], new Vector3(-0.45f, 1.45f, 0f));
            b["LeftHand"] = Node("LHand", b["LeftLowerArm"], new Vector3(-0.7f, 1.45f, 0f));
            b["RightShoulder"] = Node("RSh", b["Chest"], new Vector3(0.05f, 1.45f, 0f));
            b["RightUpperArm"] = Node("RUpArm", b["RightShoulder"], new Vector3(0.18f, 1.45f, 0f));
            b["RightLowerArm"] = Node("RLoArm", b["RightUpperArm"], new Vector3(0.45f, 1.45f, 0f));
            b["RightHand"] = Node("RHand", b["RightLowerArm"], new Vector3(0.7f, 1.45f, 0f));

            Rotated(b["LeftUpperLeg"], Vector3.forward, -5f, b["LeftUpperLeg"]);
            Rotated(b["RightUpperLeg"], Vector3.forward, 5f, b["RightUpperLeg"]);
            Rotated(b["LeftLowerLeg"], Vector3.right, 11f, b["LeftLowerLeg"]);
            Rotated(b["RightLowerLeg"], Vector3.right, 11f, b["RightLowerLeg"]);
            Rotated(b["LeftUpperArm"], Vector3.forward, 9f, b["LeftUpperArm"]);
            Rotated(b["RightUpperArm"], Vector3.forward, -9f, b["RightUpperArm"]);
            Rotated(b["Spine"], Vector3.right, 4f, b["Spine"]);
            return b;
        }

        [Test]
        public void TPose_MeasuresTheCrookedBindPose()
        {
            Dictionary<string, Transform> rig = CrookedRig();
            Dictionary<string, float> angles = TPoseSolver.Measure(n => rig.TryGetValue(n, out Transform t) ? t : null);

            Assert.AreEqual(5f, angles["L UpperLeg"], 0.01f);
            Assert.AreEqual(5f, angles["R UpperLeg"], 0.01f);
            Assert.AreEqual(9f, angles["L UpperArm"], 0.01f);
            Assert.AreEqual(9f, angles["R UpperArm"], 0.01f);
            Assert.AreEqual(4f, angles["Spine"], 0.01f);
        }

        [Test]
        public void TPose_SolveStraightensEveryLimb()
        {
            Dictionary<string, Transform> rig = CrookedRig();
            System.Func<string, Transform> bone = n => rig.TryGetValue(n, out Transform t) ? t : null;

            TPoseSolver.Solve(bone);
            Dictionary<string, float> angles = TPoseSolver.Measure(bone);

            foreach (KeyValuePair<string, float> kv in angles)
                Assert.Less(kv.Value, 0.01f, kv.Key);
            Assert.That((rig["RightHand"].position - rig["RightUpperArm"].position).normalized, Is.EqualTo(Vector3.right).Using(new Vector3Comparer(1e-4f)));
            Assert.That((rig["LeftFoot"].position - rig["LeftUpperLeg"].position).normalized, Is.EqualTo(Vector3.down).Using(new Vector3Comparer(1e-4f)));
        }

        [Test]
        public void TPose_KeepsBoneLengths()
        {
            Dictionary<string, Transform> rig = CrookedRig();
            float shin = (rig["LeftFoot"].position - rig["LeftLowerLeg"].position).magnitude;
            float forearm = (rig["RightHand"].position - rig["RightLowerArm"].position).magnitude;

            TPoseSolver.Solve(n => rig.TryGetValue(n, out Transform t) ? t : null);

            Assert.AreEqual(shin, (rig["LeftFoot"].position - rig["LeftLowerLeg"].position).magnitude, 1e-5f);
            Assert.AreEqual(forearm, (rig["RightHand"].position - rig["RightLowerArm"].position).magnitude, 1e-5f);
        }

        [Test]
        public void TPose_SkipsMissingOptionalBones()
        {
            Dictionary<string, Transform> rig = CrookedRig();
            rig.Remove("Chest");
            rig.Remove("LeftShoulder");

            Assert.DoesNotThrow(() => TPoseSolver.Solve(n => rig.TryGetValue(n, out Transform t) ? t : null));
            Assert.Less(PipelineMath.AngleOff(rig["LeftUpperArm"], rig["LeftLowerArm"], Vector3.left), 0.01f);
        }

        [TestCase(0.02f, -0.03f, 0.03f)]
        [TestCase(0.05f, 0.01f, -0.01f)]
        [TestCase(0f, 0f, 0f)]
        [TestCase(-0.027f, 0.061f, 0.027f)]
        public void GroundOffset_RaisesTheLowerSoleToTheFloor(float left, float right, float expected)
        {
            Assert.AreEqual(expected, PipelineMath.GroundOffset(left, right), 1e-6f);
        }

        [Test]
        public void GroundOffset_LeavesTheLowerSoleAtZero()
        {
            float offset = PipelineMath.GroundOffset(-0.044f, 0.012f);
            Assert.AreEqual(0f, Mathf.Min(-0.044f, 0.012f) + offset, 1e-6f);
        }

        [Test]
        public void ScaleForHeight_MatchesTheTarget()
        {
            Assert.AreEqual(1.744f, 1.0f * PipelineMath.ScaleForHeight(1.0f, 1.744f), 1e-6f);
            Assert.AreEqual(1.744f, 0.95f * PipelineMath.ScaleForHeight(0.95f, 1.744f), 1e-6f);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => PipelineMath.ScaleForHeight(0f, 1.744f));
        }

        [Test]
        public void HemHipShare_RunsFromTopToBottomShare()
        {
            Assert.AreEqual(1f, PipelineMath.HemHipShare(0.56f, 0.42f, 0.56f, 0.7f), 1e-6f);
            Assert.AreEqual(0.7f, PipelineMath.HemHipShare(0.42f, 0.42f, 0.56f, 0.7f), 1e-6f);
            Assert.AreEqual(0.85f, PipelineMath.HemHipShare(0.49f, 0.42f, 0.56f, 0.7f), 1e-5f);
            Assert.AreEqual(0.7f, PipelineMath.HemHipShare(0.30f, 0.42f, 0.56f, 0.7f), 1e-6f);
        }

        [Test]
        public void MoveToBone_ShiftsLegWeightAndStaysNormalised()
        {
            var leg = new HashSet<int> { 3, 4 };
            var w = new BoneWeight { boneIndex0 = 3, weight0 = 0.6f, boneIndex1 = 4, weight1 = 0.3f, boneIndex2 = 7, weight2 = 0.1f };

            BoneWeight r = HemWeights.MoveToBone(w, leg, 1, 0.5f, out float moved);

            Assert.AreEqual(0.45f, moved, 1e-5f);
            var map = new Dictionary<int, float> { [r.boneIndex0] = r.weight0, [r.boneIndex1] = r.weight1, [r.boneIndex2] = r.weight2, [r.boneIndex3] = r.weight3 };
            Assert.AreEqual(0.45f, map[1], 1e-5f);
            Assert.AreEqual(0.30f, map[3], 1e-5f);
            Assert.AreEqual(0.15f, map[4], 1e-5f);
            Assert.AreEqual(1f, r.weight0 + r.weight1 + r.weight2 + r.weight3, 1e-5f);
        }

        [Test]
        public void WeldSeams_GivesSplitVerticesOneWeightSet()
        {
            var positions = new[] { new Vector3(0f, 1f, 0f), new Vector3(0f, 1f, 0f), new Vector3(1f, 0f, 0f) };
            var weights = new[]
            {
                new BoneWeight { boneIndex0 = 2, weight0 = 1f },
                new BoneWeight { boneIndex0 = 5, weight0 = 0.6f, boneIndex1 = 2, weight1 = 0.4f },
                new BoneWeight { boneIndex0 = 9, weight0 = 1f },
            };

            int differing = HemWeights.WeldSeams(positions, weights);

            Assert.AreEqual(1, differing);
            Assert.AreEqual(weights[0], weights[1]);
            Assert.AreEqual(2, weights[0].boneIndex0);
            Assert.AreEqual(0.7f, weights[0].weight0, 1e-5f);
            Assert.AreEqual(5, weights[0].boneIndex1);
            Assert.AreEqual(0.3f, weights[0].weight1, 1e-5f);
            Assert.AreEqual(9, weights[2].boneIndex0, "a vertex on its own is left alone");
            Assert.AreEqual(1f, weights[2].weight0, 1e-6f);
        }

        [Test]
        public void RayTriangle_HitsInsideAndMissesOutside()
        {
            Vector3 a = new Vector3(-1f, -1f, 1f), b = new Vector3(1f, -1f, 1f), c = new Vector3(0f, 1f, 1f);
            Assert.AreEqual(1f, PipelineMath.RayTriangle(Vector3.zero, Vector3.forward, a, b, c), 1e-5f);
            Assert.AreEqual(-1f, PipelineMath.RayTriangle(new Vector3(2f, 0f, 0f), Vector3.forward, a, b, c));
            Assert.AreEqual(-1f, PipelineMath.RayTriangle(Vector3.zero, Vector3.back, a, b, c));
        }

        sealed class Vector3Comparer : IEqualityComparer<Vector3>
        {
            readonly float tolerance;
            public Vector3Comparer(float tolerance) { this.tolerance = tolerance; }
            public bool Equals(Vector3 x, Vector3 y) => (x - y).magnitude <= tolerance;
            public int GetHashCode(Vector3 v) => 0;
        }
    }
}
