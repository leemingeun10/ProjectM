using Game.Client.Battle;
using Game.Sim.Shape;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    public class TechniqueReplayTimingTests
    {
        private const float Eps = 1e-4f;

        private static TechniqueClip Clip(string id)
        {
            Assert.IsTrue(TechniqueClipCatalog.TryGet(id, 1, out TechniqueClip clip), id);
            return clip;
        }

        [TestCase(ShapeTemplates.TriangleId, TechniqueClipCatalog.SamjaeStage1State)]
        [TestCase(ShapeTemplates.FlowerId, TechniqueClipCatalog.MaehwaStage1State)]
        [TestCase(ShapeTemplates.SwordId, TechniqueClipCatalog.JewangStage1State)]
        [TestCase(ShapeTemplates.CrescentId, TechniqueClipCatalog.WolyeongStage1State)]
        public void Catalog_EachTechniqueHasItsStage1Clip(string id, string state)
        {
            Assert.AreEqual(state, Clip(id).State);
            Assert.That(Clip(id).Strike, Is.GreaterThan(0f).And.LessThan(Clip(id).Length));
        }

        [Test]
        public void Catalog_UnknownIdsAndMissingStagesHaveNoClip()
        {
            Assert.IsFalse(TechniqueClipCatalog.TryGet("circle", 1, out _));
            Assert.IsFalse(TechniqueClipCatalog.TryGet(null, 1, out _));
            Assert.IsFalse(TechniqueClipCatalog.TryGet(ShapeTemplates.SwordId, 2, out _));
            Assert.IsFalse(TechniqueClipCatalog.TryGet(ShapeTemplates.SwordId, 0, out _));
            Assert.IsNull(TechniqueClipCatalog.For(null));
            Assert.IsNull(TechniqueClipCatalog.For(new TechniqueCast(0, ShapeTemplates.SwordId, 2)));
            Assert.AreEqual(TechniqueClipCatalog.JewangStage1State, TechniqueClipCatalog.For(new TechniqueCast(0, ShapeTemplates.SwordId, 1))?.State);
        }

        // CharacterMotionPlayer finds a clip by its state name and waits out its length, so the
        // catalog's state, clip name and length must all match the asset.
        [TestCase(ShapeTemplates.TriangleId)]
        [TestCase(ShapeTemplates.FlowerId)]
        [TestCase(ShapeTemplates.SwordId)]
        [TestCase(ShapeTemplates.CrescentId)]
        public void Catalog_MatchesTheClipAsset(string id)
        {
            TechniqueClip clip = Clip(id);
            var asset = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/CharacterArt/Combat/{clip.State}.anim");
            Assert.IsNotNull(asset, clip.State);
            Assert.AreEqual(clip.State, asset.name);
            Assert.AreEqual(clip.Length, asset.length, 1f / 60f);
            Assert.IsFalse(asset.isLooping);
        }

        [Test]
        public void Find_PicksTheTurnsCast()
        {
            var casts = new[] { new TechniqueCast(2, ShapeTemplates.FlowerId, 1), new TechniqueCast(5, ShapeTemplates.SwordId, 1), new TechniqueCast(5, ShapeTemplates.TriangleId, 1) };
            Assert.AreEqual(ShapeTemplates.FlowerId, TechniqueCast.Find(casts, 2)?.TechniqueId);
            Assert.AreEqual(ShapeTemplates.SwordId, TechniqueCast.Find(casts, 5)?.TechniqueId);   // first one wins
            Assert.IsNull(TechniqueCast.Find(casts, 0));
            Assert.IsNull(TechniqueCast.Find(null, 2));
        }

        [Test]
        public void Timing_NoTechniqueIsTheUnchangedNormalTurn()
        {
            ReplayTurnTiming t = ReplayTurnTiming.For(null, null);
            Assert.IsFalse(t.IsTechniqueTurn);
            Assert.AreEqual(1.6f, t.Interval, Eps);
            Assert.AreEqual(0.48f, t.Impact, Eps);
            Assert.AreEqual(1.6f * 0.65f, t.FocusEnd, Eps);
            Assert.AreEqual(0f, t.StartDelayA);
            Assert.AreEqual(0f, t.StartDelayB);
        }

        [TestCase(ShapeTemplates.TriangleId)]
        [TestCase(ShapeTemplates.FlowerId)]
        [TestCase(ShapeTemplates.SwordId)]
        [TestCase(ShapeTemplates.CrescentId)]
        public void Timing_OneSideTechnique_ImpactOnItsStrike_IntervalIsClipPlusTail(string id)
        {
            TechniqueClip clip = Clip(id);
            foreach (bool onA in new[] { true, false })
            {
                ReplayTurnTiming t = onA ? ReplayTurnTiming.For(clip, null) : ReplayTurnTiming.For(null, clip);
                float techDelay = onA ? t.StartDelayA : t.StartDelayB;
                float otherDelay = onA ? t.StartDelayB : t.StartDelayA;

                Assert.IsTrue(t.IsTechniqueTurn);
                Assert.That(techDelay, Is.GreaterThanOrEqualTo(0f));
                Assert.That(otherDelay, Is.GreaterThanOrEqualTo(0f));
                // Both strikes land on the impact: the technique's main strike and the normal clip's 0.48 s.
                Assert.AreEqual(t.Impact, techDelay + clip.Strike, Eps);
                Assert.AreEqual(t.Impact, otherDelay + ReplayTurnTiming.NormalImpact, Eps);
                Assert.AreEqual(techDelay + clip.Length + ReplayTurnTiming.TechniqueTail, t.Interval, Eps);
                Assert.That(t.FocusEnd, Is.GreaterThan(t.Impact).And.LessThan(t.Interval));
            }
        }

        [Test]
        public void Timing_LateStrike_TechniqueStartsAtOnce_OtherSideWaits()
        {
            TechniqueClip jewang = Clip(ShapeTemplates.SwordId);   // strike 1.6 s
            ReplayTurnTiming t = ReplayTurnTiming.For(jewang, null);
            Assert.AreEqual(0f, t.StartDelayA, Eps);
            Assert.AreEqual(jewang.Strike, t.Impact, Eps);
            Assert.AreEqual(jewang.Strike - 0.48f, t.StartDelayB, Eps);
            Assert.AreEqual(jewang.Length + 0.4f, t.Interval, Eps);
        }

        [Test]
        public void Timing_EarlyStrike_TechniqueWaitsForTheNormalStrike()
        {
            // A strike before 0.48 s would need the normal clip to start before the turn, so the
            // technique waits instead and the impact stays at 0.48 s.
            var early = new TechniqueClip("Early", 2f, 0.3f);
            ReplayTurnTiming t = ReplayTurnTiming.For(null, early);
            Assert.AreEqual(0.48f, t.Impact, Eps);
            Assert.AreEqual(0f, t.StartDelayA, Eps);
            Assert.AreEqual(0.18f, t.StartDelayB, Eps);
            Assert.AreEqual(0.18f + 2f + 0.4f, t.Interval, Eps);
        }

        private static readonly string[] AllIds =
            { ShapeTemplates.TriangleId, ShapeTemplates.FlowerId, ShapeTemplates.SwordId, ShapeTemplates.CrescentId };

        [Test]
        public void Timing_BothSides_StrikesMeetOnTheLaterOne_TurnCoversBothClips()
        {
            foreach (string idA in AllIds)
            foreach (string idB in AllIds)
            {
                TechniqueClip a = Clip(idA);
                TechniqueClip b = Clip(idB);
                ReplayTurnTiming t = ReplayTurnTiming.For(a, b);
                Assert.AreEqual(Mathf.Max(a.Strike, b.Strike), t.Impact, Eps, $"{idA} vs {idB}");
                Assert.AreEqual(t.Impact, t.StartDelayA + a.Strike, Eps);
                Assert.AreEqual(t.Impact, t.StartDelayB + b.Strike, Eps);
                Assert.IsTrue(t.StartDelayA < Eps || t.StartDelayB < Eps, "the later strike's side starts at once");
                float end = Mathf.Max(t.StartDelayA + a.Length, t.StartDelayB + b.Length);
                Assert.AreEqual(end + 0.4f, t.Interval, Eps);
            }
        }

        // Every technique turn, one side or both, fits in MaxTurnSeconds with the current cuts.
        [Test]
        public void Timing_EveryTechniqueTurnFitsTheCap()
        {
            foreach (string idA in AllIds)
            {
                Assert.That(ReplayTurnTiming.For(Clip(idA), null).Interval, Is.LessThanOrEqualTo(TechniqueClipCatalog.MaxTurnSeconds), idA);
                Assert.That(ReplayTurnTiming.For(null, Clip(idA)).Interval, Is.LessThanOrEqualTo(TechniqueClipCatalog.MaxTurnSeconds), idA);
                foreach (string idB in AllIds)
                    Assert.That(ReplayTurnTiming.For(Clip(idA), Clip(idB)).Interval, Is.LessThanOrEqualTo(TechniqueClipCatalog.MaxTurnSeconds), $"{idA} vs {idB}");
            }
        }
    }
}
