using System;
using System.Collections.Generic;
using System.Text;

namespace BottleSort.Core
{
    // xoshiro256** с засевом через SplitMix64. System.Random и UnityEngine.Random не годятся:
    // их реализация может отличаться между платформами и версиями, а уровни должны совпадать бит в бит.
    public sealed class Rng
    {
        private ulong s0, s1, s2, s3;

        public Rng(ulong seed)
        {
            ulong state = seed;
            s0 = SplitMix(ref state);
            s1 = SplitMix(ref state);
            s2 = SplitMix(ref state);
            s3 = SplitMix(ref state);
        }

        public ulong NextULong()
        {
            ulong result = RotateLeft(s1 * 5, 7) * 9;
            ulong t = s1 << 17;
            s2 ^= s0;
            s3 ^= s1;
            s1 ^= s2;
            s0 ^= s3;
            s2 ^= t;
            s3 = RotateLeft(s3, 45);
            return result;
        }

        // Без перекоса по модулю: значения из «хвоста», который не делится на bound, перебрасываем.
        public int NextInt(int bound)
        {
            if (bound <= 0)
                throw new ArgumentOutOfRangeException(nameof(bound));
            ulong range = (ulong)bound;
            ulong limit = ulong.MaxValue - (ulong.MaxValue % range + 1) % range;
            ulong value;
            do
                value = NextULong();
            while (value > limit);
            return (int)(value % range);
        }

        public int NextInt(int min, int maxExclusive) => min + NextInt(maxExclusive - min);

        public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

        public bool Chance(double probability) => NextDouble() < probability;

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = NextInt(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public T Pick<T>(IReadOnlyList<T> list) => list[NextInt(list.Count)];

        private static ulong SplitMix(ref ulong state)
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        private static ulong RotateLeft(ulong value, int bits) => (value << bits) | (value >> (64 - bits));

        public static ulong Mix(ulong value)
        {
            ulong state = value;
            return SplitMix(ref state);
        }
    }

    // seed(L, k) = hash64("bs", genVersion, L, k) из LEVEL_DESIGN.md §9.4.
    public static class Seeds
    {
        public static ulong Combine(ulong seed, long value) => Rng.Mix(seed ^ Rng.Mix((ulong)value));

        public static ulong FromText(string text)
        {
            // FNV-1a по UTF-8: не зависит от string.GetHashCode, который в .NET рандомизирован.
            ulong hash = 14695981039346656037UL;
            foreach (byte b in Encoding.UTF8.GetBytes(text))
            {
                hash ^= b;
                hash *= 1099511628211UL;
            }
            return Rng.Mix(hash);
        }

        public static ulong ForLevel(int genVersion, int level) =>
            Combine(Combine(FromText("bs"), genVersion), level);

        public static ulong ForCandidate(ulong levelSeed, int candidate) => Combine(levelSeed, candidate);

        public static ulong ForDaily(int genVersion, int year, int month, int day) =>
            Combine(FromText("daily"), genVersion * 100_000_000L + year * 10_000L + month * 100L + day);
    }
}
