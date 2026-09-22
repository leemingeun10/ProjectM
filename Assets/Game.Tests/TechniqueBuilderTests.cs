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
                TechniqueAddResult result = builder.TryAddStep(Stance.Stand, false, DummyStroke(), DummyPoints());
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
                builder.TryAddStep(Stance.Stand, false, DummyStroke(), DummyPoints());

            TechniqueAddResult result = builder.TryAddStep(Stance.Stand, false, DummyStroke(), DummyPoints());

            Assert.AreEqual(TechniqueAddResult.SequenceFull, result);
            Assert.AreEqual(TechniqueBuilder.MaxTurns, builder.TurnCount);
        }

        [Test]
        public void ThirdCounterStepIsRejected()
        {
            var builder = new TechniqueBuilder();

            Assert.AreEqual(TechniqueAddResult.Success, builder.TryAddStep(Stance.Stand, true, DummyStroke(), DummyPoints()));
            Assert.AreEqual(TechniqueAddResult.Success, builder.TryAddStep(Stance.Stand, true, DummyStroke(), DummyPoints()));
            Assert.AreEqual(0, builder.RemainingCounters);

            TechniqueAddResult result = builder.TryAddStep(Stance.Stand, true, DummyStroke(), DummyPoints());

            Assert.AreEqual(TechniqueAddResult.CounterLimitReached, result);
            Assert.AreEqual(2, builder.TurnCount);
        }

        [Test]
        public void TurnIndexMatchesInsertionOrder()
        {
            var builder = new TechniqueBuilder();
            for (int i = 0; i < 5; i++)
                builder.TryAddStep(Stance.Advance, false, DummyStroke(), DummyPoints());

            for (int i = 0; i < builder.TurnCount; i++)
                Assert.AreEqual(i, builder.Steps[i].TurnIndex);
        }
    }
}
