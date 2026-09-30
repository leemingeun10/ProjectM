using System;
using Game.Sim.Common;
using NUnit.Framework;

namespace Game.Tests
{
    public class DeterministicRngTests
    {
        [Test]
        public void SeedZero_MatchesSplitMix64ReferenceOutput()
        {
            // Reference SplitMix64 values for seed 0; pins the algorithm so it can never drift.
            var rng = new DeterministicRng(0);
            Assert.AreEqual(0xE220A8397B1DCDAFUL, rng.NextULong());
            Assert.AreEqual(0x6E789E6AA1B965F4UL, rng.NextULong());
        }

        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            var a = new DeterministicRng(12345);
            var b = new DeterministicRng(12345);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(a.NextULong(), b.NextULong());
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentSequences()
        {
            Assert.AreNotEqual(new DeterministicRng(1).NextULong(), new DeterministicRng(2).NextULong());
        }

        [Test]
        public void Range_StaysWithinBounds()
        {
            var rng = new DeterministicRng(7);
            for (int i = 0; i < 1000; i++)
            {
                int v = rng.Range(-5, 5);
                Assert.GreaterOrEqual(v, -5);
                Assert.Less(v, 5);
            }
            Assert.AreEqual(3, rng.Range(3, 4));
        }

        [Test]
        public void Range_EmptyThrows()
        {
            var rng = new DeterministicRng(7);
            Assert.Throws<ArgumentException>(() => rng.Range(5, 5));
        }

        [Test]
        public void Chance_ZeroAndHundredAreAbsolute()
        {
            var rng = new DeterministicRng(99);
            for (int i = 0; i < 200; i++)
            {
                Assert.IsFalse(rng.Chance(0));
                Assert.IsTrue(rng.Chance(100));
            }
        }
    }
}
