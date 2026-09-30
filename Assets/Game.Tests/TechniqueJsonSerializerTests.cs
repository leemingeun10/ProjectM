using System.Collections.Generic;
using Game.Sim.Stroke;
using Game.Sim.Technique;
using NUnit.Framework;

namespace Game.Tests
{
    public class TechniqueJsonSerializerTests
    {
        // Raw values deliberately include negatives, non-multiples of Fixed.Scale and values
        // outside int range so any lossy (float/int) path would show up as a mismatch.
        private static TechniqueBuilder BuildMixedBuilder()
        {
            var builder = new TechniqueBuilder();

            Assert.AreEqual(TechniqueAddResult.Success, builder.TryAddStep(false,
                StrokeResult.Slash(Fixed.FromRaw(123456), Fixed.FromRaw(707), Fixed.FromRaw(-707), Fixed.FromRaw(-1000), Fixed.FromRaw(1)),
                new List<StrokePoint> { new StrokePoint(0, 0), new StrokePoint(1001, -2002), new StrokePoint(3003, 4004) }));

            Assert.AreEqual(TechniqueAddResult.Success, builder.TryAddStep(true,
                StrokeResult.Thrust(Fixed.FromRaw(98765432109L)),
                new List<StrokePoint> { new StrokePoint(-1, -1), new StrokePoint(500, 999), new StrokePoint(-1, 0), new StrokePoint(-1, -1) }));

            Assert.AreEqual(TechniqueAddResult.Success, builder.TryAddStep(false,
                StrokeResult.Slash(Fixed.FromRaw(1), Fixed.Zero, Fixed.One, Fixed.FromRaw(333), Fixed.FromRaw(-943)),
                new List<StrokePoint> { new StrokePoint(long.MaxValue / 4, long.MinValue / 4), new StrokePoint(7, 7) }));

            Assert.AreEqual(TechniqueAddResult.Success, builder.TryAddStep(false,
                StrokeResult.Thrust(Fixed.Zero),
                new List<StrokePoint> { new StrokePoint(10, 20) }));

            Assert.AreEqual(TechniqueAddResult.Success, builder.TryAddStep(true,
                StrokeResult.Slash(Fixed.FromRaw(4_000_000_001L), Fixed.FromRaw(-1), Fixed.FromRaw(-999), Fixed.FromRaw(999), Fixed.FromRaw(-1)),
                new List<StrokePoint> { new StrokePoint(-3, 4), new StrokePoint(5, -6) }));

            Assert.AreEqual(TechniqueAddResult.Success, builder.TryAddStep(false,
                StrokeResult.Slash(Fixed.FromInt(42), Fixed.One, Fixed.Zero, Fixed.One, Fixed.Zero),
                new List<StrokePoint>()));

            Assert.AreEqual(TechniqueAddResult.Success, builder.TryAddStep(false,
                StrokeResult.Thrust(Fixed.FromRaw(12345)),
                new List<StrokePoint> { new StrokePoint(1, 2), new StrokePoint(3, 4), new StrokePoint(5, 6), new StrokePoint(1, 2) }));

            return builder;
        }

        private static void AssertStepsEqual(IReadOnlyList<TechniqueStep> expected, IReadOnlyList<TechniqueStep> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count, "step count");
            for (int i = 0; i < expected.Count; i++)
            {
                TechniqueStep e = expected[i];
                TechniqueStep a = actual[i];
                string at = $"step {i}";

                Assert.AreEqual(e.TurnIndex, a.TurnIndex, at + " turnIndex");
                Assert.AreEqual(e.IsCounter, a.IsCounter, at + " isCounter");
                Assert.AreEqual(e.Stroke.Type, a.Stroke.Type, at + " strokeType");
                Assert.AreEqual(e.Stroke.Length.Raw, a.Stroke.Length.Raw, at + " length");
                Assert.AreEqual(e.Stroke.StartDirectionX.Raw, a.Stroke.StartDirectionX.Raw, at + " startDirectionX");
                Assert.AreEqual(e.Stroke.StartDirectionY.Raw, a.Stroke.StartDirectionY.Raw, at + " startDirectionY");
                Assert.AreEqual(e.Stroke.EndDirectionX.Raw, a.Stroke.EndDirectionX.Raw, at + " endDirectionX");
                Assert.AreEqual(e.Stroke.EndDirectionY.Raw, a.Stroke.EndDirectionY.Raw, at + " endDirectionY");
                Assert.AreEqual(e.Stroke.Area.Raw, a.Stroke.Area.Raw, at + " area");

                Assert.AreEqual(e.RawPoints.Count, a.RawPoints.Count, at + " rawPoints count");
                for (int j = 0; j < e.RawPoints.Count; j++)
                {
                    Assert.AreEqual(e.RawPoints[j].X.Raw, a.RawPoints[j].X.Raw, $"{at} rawPoints[{j}].x");
                    Assert.AreEqual(e.RawPoints[j].Y.Raw, a.RawPoints[j].Y.Raw, $"{at} rawPoints[{j}].y");
                }
            }
        }

        [Test]
        public void RoundTrip_MixedSteps_RestoresEveryFieldExactly()
        {
            TechniqueBuilder builder = BuildMixedBuilder();

            string json = TechniqueJsonSerializer.Serialize(builder);
            (int version, List<TechniqueStep> steps) = TechniqueJsonSerializer.Deserialize(json);

            Assert.AreEqual(TechniqueJsonSerializer.CurrentVersion, version);
            AssertStepsEqual(builder.Steps, steps);
        }

        [Test]
        public void Version_IsWrittenAndParsedAsTwo()
        {
            string json = TechniqueJsonSerializer.Serialize(BuildMixedBuilder());

            StringAssert.Contains("\"version\": 2", json);
            Assert.AreEqual(2, TechniqueJsonSerializer.Deserialize(json).version);
        }

        [Test]
        public void Serialize_DoesNotWriteStance()
        {
            string json = TechniqueJsonSerializer.Serialize(BuildMixedBuilder());

            StringAssert.DoesNotContain("stance", json);
        }

        [Test]
        public void LegacyV1WithStance_LoadsAndIgnoresStance()
        {
            const string v1 =
                "{\"version\": 1, \"steps\": [" +
                "{\"turnIndex\": 0, \"stance\": \"Sit\", \"isCounter\": true, \"strokeType\": \"Thrust\", \"area\": 5000," +
                " \"rawPoints\": [{\"x\": 1, \"y\": 2}]}]}";

            (int version, List<TechniqueStep> steps) = TechniqueJsonSerializer.Deserialize(v1);

            Assert.AreEqual(1, version);
            Assert.AreEqual(1, steps.Count);
            Assert.IsTrue(steps[0].IsCounter);
            Assert.AreEqual(StrokeType.Thrust, steps[0].Stroke.Type);
            Assert.AreEqual(5000, steps[0].Stroke.Area.Raw);
            Assert.AreEqual(2, steps[0].RawPoints[0].Y.Raw);
        }

        [Test]
        public void UnsupportedVersion_Throws()
        {
            Assert.Throws<TechniqueJsonException>(() => TechniqueJsonSerializer.Deserialize("{\"version\": 3, \"steps\": []}"));
            Assert.Throws<TechniqueJsonException>(() => TechniqueJsonSerializer.Deserialize("{\"version\": 0, \"steps\": []}"));
            Assert.Throws<TechniqueJsonException>(() => TechniqueJsonSerializer.Deserialize("{\"steps\": []}"));
        }

        [Test]
        public void MalformedJson_Throws()
        {
            Assert.Throws<TechniqueJsonException>(() => TechniqueJsonSerializer.Deserialize("{\"version\": 1, \"steps\": [}"));
            Assert.Throws<TechniqueJsonException>(() => TechniqueJsonSerializer.Deserialize("{\"version\": 1.5, \"steps\": []}"));
        }

        [Test]
        public void StepWithEmptyRawPoints_RoundTrips()
        {
            var builder = new TechniqueBuilder();
            builder.TryAddStep(false, StrokeResult.Thrust(Fixed.FromRaw(777)), new List<StrokePoint>());
            builder.TryAddStep(true,
                StrokeResult.Slash(Fixed.FromRaw(5), Fixed.One, Fixed.Zero, Fixed.Zero, Fixed.One), null);

            (_, List<TechniqueStep> steps) = TechniqueJsonSerializer.Deserialize(TechniqueJsonSerializer.Serialize(builder));

            Assert.AreEqual(0, steps[0].RawPoints.Count);
            Assert.AreEqual(0, steps[1].RawPoints.Count);
            AssertStepsEqual(builder.Steps, steps);
        }

        [Test]
        public void EmptyBuilder_RoundTripsWithoutException()
        {
            var builder = new TechniqueBuilder();

            string json = null;
            Assert.DoesNotThrow(() => json = TechniqueJsonSerializer.Serialize(builder));

            (int version, List<TechniqueStep> steps) result = default;
            Assert.DoesNotThrow(() => result = TechniqueJsonSerializer.Deserialize(json));

            Assert.AreEqual(TechniqueJsonSerializer.CurrentVersion, result.version);
            Assert.IsNotNull(result.steps);
            Assert.AreEqual(0, result.steps.Count);
        }
    }
}
