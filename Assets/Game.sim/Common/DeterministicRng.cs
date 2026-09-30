using System;

namespace Game.Sim.Common
{
    // Seeded SplitMix64 generator. Pure integer math, so the same seed yields the same sequence
    // on every device (System.Random's algorithm is not guaranteed across runtimes).
    public sealed class DeterministicRng
    {
        private ulong state;

        public DeterministicRng(ulong seed)
        {
            state = seed;
        }

        public ulong NextULong()
        {
            ulong z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        // Uniform-enough integer in [minInclusive, maxExclusive). The modulo bias is negligible
        // for the small ranges used here and, more importantly, fully deterministic.
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentException($"Empty range [{minInclusive}, {maxExclusive}).");

            ulong span = (ulong)((long)maxExclusive - minInclusive);
            return (int)(minInclusive + (long)(NextULong() % span));
        }

        // True with the given probability in percent (0..100).
        public bool Chance(int percent) => Range(0, 100) < percent;
    }
}
