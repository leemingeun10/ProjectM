using Game.Client.Battle;
using Game.Sim.Shape;
using Game.Sim.Technique;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    // Long technique turns (TechniqueCast + ReplayTurnTiming). The catalog has no clips for now, so
    // the timing rules are checked on made-up clips shaped like the parked first cuts.
    public class TechniqueReplayTimingTests
    {
        private const float Eps = 1e-4f;

        private static readonly TechniqueClip LateStrike = new TechniqueClip("Late", 2.2f, 1.6f);
        private static readonly TechniqueClip LateLong = new TechniqueClip("LateLong", 2.3f, 1.983f);
        private static readonly TechniqueClip ShortTail = new TechniqueClip("ShortTail", 2.15f, 1.833f);
        private static readonly TechniqueClip EarlyStrike = new TechniqueClip("Early", 2f, 0.3f);

        private static readonly TechniqueClip[] Clips = { LateStrike, LateLong, ShortTail, EarlyStrike };

        [Test]
        public void Catalog_HasNoClipsForNow()
        {
            foreach (HiddenTechniqueDefinition d in HiddenTechniques.All)
            {
                Assert.IsFalse(TechniqueClipCatalog.TryGet(d.Id, 1, out _), d.Id);
                Assert.IsNull(TechniqueClipCatalog.For(new TechniqueCast(0, d.Id, 1)), d.Id);
            }
            Assert.IsNull(TechniqueClipCatalog.For(null));
        }

        // Guards future entries: any technique turn the catalog can make, one side or both, fits the cap.
        [Test]
        public void Catalog_EveryTechniqueTurnFitsTheCap()
        {
            foreach (HiddenTechniqueDefinition a in HiddenTechniques.All)
            {
                TechniqueClip? clipA = TechniqueClipCatalog.For(new TechniqueCast(0, a.Id, 1));
                if (clipA == null)
                    continue;
                Assert.That(ReplayTurnTiming.For(clipA, null).Interval, Is.LessThanOrEqualTo(TechniqueClipCatalog.MaxTurnSeconds), a.Id);
                foreach (HiddenTechniqueDefinition b in HiddenTechniques.All)
                {
                    TechniqueClip? clipB = TechniqueClipCatalog.For(new TechniqueCast(0, b.Id, 1));
                    if (clipB != null)
                        Assert.That(ReplayTurnTiming.For(clipA, clipB).Interval, Is.LessThanOrEqualTo(TechniqueClipCatalog.MaxTurnSeconds), $"{a.Id} vs {b.Id}");
                }
            }
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

        [Test]
        public void Timing_OneSideTechnique_ImpactOnItsStrike_IntervalIsClipPlusTail()
        {
            foreach (TechniqueClip clip in Clips)
            foreach (bool onA in new[] { true, false })
            {
                ReplayTurnTiming t = onA ? ReplayTurnTiming.For(clip, null) : ReplayTurnTiming.For(null, clip);
                float techDelay = onA ? t.StartDelayA : t.StartDelayB;
                float otherDelay = onA ? t.StartDelayB : t.StartDelayA;
                string at = $"{clip.State} on {(onA ? "A" : "B")}";

                Assert.IsTrue(t.IsTechniqueTurn, at);
                Assert.That(techDelay, Is.GreaterThanOrEqualTo(0f), at);
                Assert.That(otherDelay, Is.GreaterThanOrEqualTo(0f), at);
                // Both strikes land on the impact: the technique's main strike and the normal clip's 0.48 s.
                Assert.AreEqual(t.Impact, techDelay + clip.Strike, Eps, at);
                Assert.AreEqual(t.Impact, otherDelay + ReplayTurnTiming.NormalImpact, Eps, at);
                Assert.AreEqual(techDelay + clip.Length + ReplayTurnTiming.TechniqueTail, t.Interval, Eps, at);
                Assert.That(t.FocusEnd, Is.GreaterThan(t.Impact).And.LessThan(t.Interval), at);
            }
        }

        [Test]
        public void Timing_LateStrike_TechniqueStartsAtOnce_OtherSideWaits()
        {
            ReplayTurnTiming t = ReplayTurnTiming.For(LateStrike, null);
            Assert.AreEqual(0f, t.StartDelayA, Eps);
            Assert.AreEqual(1.6f, t.Impact, Eps);
            Assert.AreEqual(1.6f - 0.48f, t.StartDelayB, Eps);
            Assert.AreEqual(2.2f + 0.4f, t.Interval, Eps);
        }

        [Test]
        public void Timing_EarlyStrike_TechniqueWaitsForTheNormalStrike()
        {
            // A strike before 0.48 s would need the normal clip to start before the turn, so the
            // technique waits instead and the impact stays at 0.48 s.
            ReplayTurnTiming t = ReplayTurnTiming.For(null, EarlyStrike);
            Assert.AreEqual(0.48f, t.Impact, Eps);
            Assert.AreEqual(0f, t.StartDelayA, Eps);
            Assert.AreEqual(0.18f, t.StartDelayB, Eps);
            Assert.AreEqual(0.18f + 2f + 0.4f, t.Interval, Eps);
        }

        [Test]
        public void Timing_BothSides_StrikesMeetOnTheLaterOne_TurnCoversBothClips()
        {
            foreach (TechniqueClip a in Clips)
            foreach (TechniqueClip b in Clips)
            {
                ReplayTurnTiming t = ReplayTurnTiming.For(a, b);
                string at = $"{a.State} vs {b.State}";
                Assert.AreEqual(Mathf.Max(a.Strike, b.Strike), t.Impact, Eps, at);
                Assert.AreEqual(t.Impact, t.StartDelayA + a.Strike, Eps, at);
                Assert.AreEqual(t.Impact, t.StartDelayB + b.Strike, Eps, at);
                Assert.IsTrue(t.StartDelayA < Eps || t.StartDelayB < Eps, "the later strike's side starts at once: " + at);
                float end = Mathf.Max(t.StartDelayA + a.Length, t.StartDelayB + b.Length);
                Assert.AreEqual(end + 0.4f, t.Interval, Eps, at);
            }
        }

        [Test]
        public void Timing_LongestBothSidesPair_IsTheLateLongVsLateStrike()
        {
            // Mirrors the old Jewang vs Maehwa case, the longest pair the parked cuts allowed: 2.983 s.
            ReplayTurnTiming t = ReplayTurnTiming.For(LateStrike, LateLong);
            Assert.AreEqual(1.983f - 1.6f + 2.2f + 0.4f, t.Interval, Eps);
        }
    }
}
