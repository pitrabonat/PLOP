using System;

namespace Plop.Generation
{
    /// <summary>
    /// SplitMix64 PRNG (Steele, Lea &amp; Flood, "Fast Splittable Pseudorandom Number Generators", 2014;
    /// constants as in the public-domain reference by Sebastiano Vigna). Small, fast, fully deterministic
    /// across platforms — unlike System.Random, whose algorithm is not guaranteed between runtimes.
    /// Never change this implementation without bumping <see cref="GeneratorInfo.Version"/>.
    /// </summary>
    public sealed class SplitMix64
    {
        private ulong _state;

        public SplitMix64(ulong seed) { _state = seed; }

        public ulong NextULong()
        {
            ulong z = (_state += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Uniform integer in [0, maxExclusive) without modulo bias (rejection sampling).</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            ulong bound = (ulong)maxExclusive;
            ulong limit = ulong.MaxValue - (ulong.MaxValue % bound);
            ulong r;
            do { r = NextULong(); } while (r >= limit);
            return (int)(r % bound);
        }

        public int NextInt(int minInclusive, int maxExclusive) => minInclusive + NextInt(maxExclusive - minInclusive);

        /// <summary>Uniform double in [0, 1) with 53 random bits.</summary>
        public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

        /// <summary>Derives an independent seed for sub-stream <paramref name="index"/> of a base seed.</summary>
        public static ulong Derive(ulong baseSeed, ulong index)
        {
            var mix = new SplitMix64(baseSeed ^ (index * 0xD1B54A32D192ED03UL));
            mix.NextULong();
            return mix.NextULong();
        }
    }
}
