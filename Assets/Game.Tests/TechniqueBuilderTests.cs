using System.Collections.Generic;
using Game.Sim.Stroke;
using Game.Sim.Technique;
using NUnit.Framework;

namespace Game.Tests
{
    public class TechniqueBuilderTests
    {
        private static StrokeResult DummyStroke()
            => StrokeResult.Slash(Fixed.FromInt(100), Fixed.One, Fixed.Zero, Fixed.One, Fixed.Zero);

        private static List<StrokePoint> DummyPoints()
            => new List<StrokePoint> { StrokePoint.FromInt(0, 0), StrokePoint.FromInt(10, 10) };

        [Test]
        public void AddsUpToTenTurnsSuccessfully()
        {
            var builder = new TechniqueBuilder();
            for (int i = 0; i < TechniqueBuilder.MaxTurns; i++)
            {
                TechniqueAddResult result = builder.TryAddStep(false, DummyStroke(), DummyPoints());
                Assert.AreEqual(TechniqueAddResult.Success, result);
            }

            Assert.AreEqual(TechniqueBuilder.MaxTurns, builder.TurnCount);
            Assert.IsTrue(builder.IsFull);
        }

        [Test]
        public void EleventhStepIsRejected()
        {
            var builder = new TechniqueBuilder();
            for (int i = 0; i < TechniqueBuilder.MaxTurns; i++)
                builder.TryAddStep(false, DummyStroke(), DummyPoints());

            TechniqueAddResult result = builder.TryAddStep(false, DummyStroke(), DummyPoints());

            Assert.AreEqual(TechniqueAddResult.SequenceFull, result);
            Assert.AreEqual(TechniqueBuilder.MaxTurns, builder.TurnCount);
        }

        [Test]
        public void ThirdCounterStepIsRejected()
        {
            var builder = new TechniqueBuilder();

            Assert.AreEqual(TechniqueAddResult.Success, builder.TryAddStep(true, DummyStroke(), DummyPoints()));
            Assert.AreEqual(TechniqueAddResult.Success, builder.TryAddStep(true, DummyStroke(), DummyPoints()));
            Assert.AreEqual(0, builder.RemainingCounters);

            TechniqueAddResult result = builder.TryAddStep(true, DummyStroke(), DummyPoints());

            Assert.AreEqual(TechniqueAddResult.CounterLimitReached, result);
            Assert.AreEqual(2, builder.TurnCount);
        }

        [Test]
        public void TurnIndexMatchesInsertionOrder()
        {
            var builder = new TechniqueBuilder();
            for (int i = 0; i < 5; i++)
                builder.TryAddStep(false, DummyStroke(), DummyPoints());

            for (int i = 0; i < builder.TurnCount; i++)
                Assert.AreEqual(i, builder.Steps[i].TurnIndex);
        }

        [Test]
        public void ResetClearsStepsAndRestoresCounters()
        {
            var builder = new TechniqueBuilder();
            builder.TryAddStep(false, DummyStroke(), DummyPoints());
            builder.TryAddStep(true, DummyStroke(), DummyPoints());
            builder.TryAddStep(false, DummyStroke(), DummyPoints());
            builder.TryAddStep(true, DummyStroke(), DummyPoints());
            Assert.AreEqual(4, builder.TurnCount);
            Assert.AreEqual(0, builder.RemainingCounters);

            builder.Reset();

            Assert.AreEqual(0, builder.TurnCount);
            Assert.AreEqual(0, builder.Steps.Count);
            Assert.AreEqual(TechniqueBuilder.MaxCounters, builder.RemainingCounters);
            Assert.IsFalse(builder.IsFull);
        }

        [Test]
        public void CanBuildFullSequenceAgainAfterReset()
        {
            var builder = new TechniqueBuilder();
            for (int i = 0; i < TechniqueBuilder.MaxTurns; i++)
                builder.TryAddStep(i < TechniqueBuilder.MaxCounters, DummyStroke(), DummyPoints());
            Assert.IsTrue(builder.IsFull);
            Assert.AreEqual(0, builder.RemainingCounters);

            builder.Reset();

            for (int i = 0; i < TechniqueBuilder.MaxTurns; i++)
            {
                bool isCounter = i < TechniqueBuilder.MaxCounters;
                Assert.AreEqual(TechniqueAddResult.Success, builder.TryAddStep(isCounter, DummyStroke(), DummyPoints()));
            }

            Assert.AreEqual(TechniqueBuilder.MaxTurns, builder.TurnCount);
            Assert.IsTrue(builder.IsFull);
            for (int i = 0; i < builder.TurnCount; i++)
                Assert.AreEqual(i, builder.Steps[i].TurnIndex);
        }

        // ---- RemoveAt ----

        // Each step's first raw point X is its original insertion order, so a step can be
        // identified after renumbering.
        private static List<StrokePoint> MarkerPoints(int marker)
            => new List<StrokePoint> { StrokePoint.FromInt(marker, 0), StrokePoint.FromInt(marker, 10) };

        private static TechniqueBuilder BuildMarked(int count)
        {
            var builder = new TechniqueBuilder();
            for (int i = 0; i < count; i++)
                builder.TryAddStep(false, DummyStroke(), MarkerPoints(i));
            return builder;
        }

        private static void AssertOrder(TechniqueBuilder builder, params int[] expectedMarkers)
        {
            Assert.AreEqual(expectedMarkers.Length, builder.TurnCount, "turn count");
            for (int i = 0; i < expectedMarkers.Length; i++)
            {
                Assert.AreEqual(i, builder.Steps[i].TurnIndex, $"TurnIndex at {i}");
                Assert.AreEqual(Fixed.FromInt(expectedMarkers[i]), builder.Steps[i].RawPoints[0].X, $"step at {i}");
            }
        }

        [Test]
        public void RemoveAt_Middle_ShiftsLaterTurnsAndRenumbers()
        {
            TechniqueBuilder builder = BuildMarked(TechniqueBuilder.MaxTurns);

            Assert.IsTrue(builder.RemoveAt(3));

            AssertOrder(builder, 0, 1, 2, 4, 5, 6, 7, 8, 9);
        }

        [Test]
        public void RemoveAt_First_ShiftsEverything()
        {
            TechniqueBuilder builder = BuildMarked(5);

            Assert.IsTrue(builder.RemoveAt(0));

            AssertOrder(builder, 1, 2, 3, 4);
        }

        [Test]
        public void RemoveAt_Last_LeavesOthersUntouched()
        {
            TechniqueBuilder builder = BuildMarked(5);

            Assert.IsTrue(builder.RemoveAt(4));

            AssertOrder(builder, 0, 1, 2, 3);
        }

        [Test]
        public void RemoveAt_OnlyStep_LeavesEmpty()
        {
            TechniqueBuilder builder = BuildMarked(1);

            Assert.IsTrue(builder.RemoveAt(0));

            Assert.AreEqual(0, builder.TurnCount);
        }

        [Test]
        public void RemoveAt_CounterStep_RestoresOneCounter()
        {
            var builder = new TechniqueBuilder();
            builder.TryAddStep(false, DummyStroke(), DummyPoints());
            builder.TryAddStep(true, DummyStroke(), DummyPoints());
            builder.TryAddStep(true, DummyStroke(), DummyPoints());
            Assert.AreEqual(0, builder.RemainingCounters);

            Assert.IsTrue(builder.RemoveAt(1));

            Assert.AreEqual(1, builder.RemainingCounters);
            Assert.AreEqual(TechniqueAddResult.Success, builder.TryAddStep(true, DummyStroke(), DummyPoints()));
            Assert.AreEqual(0, builder.RemainingCounters);
        }

        [Test]
        public void RemoveAt_NonCounterStep_DoesNotRestoreCounter()
        {
            var builder = new TechniqueBuilder();
            builder.TryAddStep(true, DummyStroke(), DummyPoints());
            builder.TryAddStep(false, DummyStroke(), DummyPoints());
            Assert.AreEqual(1, builder.RemainingCounters);

            Assert.IsTrue(builder.RemoveAt(1));

            Assert.AreEqual(1, builder.RemainingCounters);
        }

        [TestCase(-1)]
        [TestCase(6)]   // == TurnCount
        [TestCase(100)]
        [TestCase(int.MinValue)]
        public void RemoveAt_OutOfRange_ReturnsFalseAndChangesNothing(int index)
        {
            TechniqueBuilder builder = BuildMarked(5);
            builder.TryAddStep(true, DummyStroke(), MarkerPoints(5));

            Assert.IsFalse(builder.RemoveAt(index));

            AssertOrder(builder, 0, 1, 2, 3, 4, 5);
            Assert.AreEqual(TechniqueBuilder.MaxCounters - 1, builder.RemainingCounters);
        }

        [Test]
        public void RemoveAt_OnEmptyBuilder_ReturnsFalse()
        {
            var builder = new TechniqueBuilder();

            Assert.IsFalse(builder.RemoveAt(0));
            Assert.AreEqual(0, builder.TurnCount);
        }

        [Test]
        public void RemoveAt_FromFull_AllowsAddingAgain()
        {
            TechniqueBuilder builder = BuildMarked(TechniqueBuilder.MaxTurns);
            Assert.IsTrue(builder.IsFull);

            Assert.IsTrue(builder.RemoveAt(3));
            Assert.IsFalse(builder.IsFull);

            Assert.AreEqual(TechniqueAddResult.Success, builder.TryAddStep(false, DummyStroke(), MarkerPoints(99)));
            Assert.IsTrue(builder.IsFull);
            AssertOrder(builder, 0, 1, 2, 4, 5, 6, 7, 8, 9, 99);
        }
    }
}
