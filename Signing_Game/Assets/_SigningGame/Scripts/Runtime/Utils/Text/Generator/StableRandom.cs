using System;
using UnityEngine;

namespace Utils.Text.Generator {
    public struct StableRandom {
        private ulong _state;

        public ulong State => _state;

        public StableRandom(ulong seed) {
            _state = seed;
        }

        public ulong NextUInt64() {
            _state += 0x9E3779B97F4A7C15UL;
            ulong value = _state;
            
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;

            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;

            return value ^ (value >> 31);
        }

        public int NextInt(int minInclusive, int maxExclusive) {
            if (maxExclusive <= minInclusive) {
                throw new ArgumentOutOfRangeException(
                    nameof(maxExclusive));
            }

            ulong range = (ulong)((long)maxExclusive - minInclusive);

            // Removes modulo bias.
            ulong threshold = unchecked(
                (0UL - range) % range);

            ulong value;

            do {
                value = NextUInt64();
            } while (value < threshold);

            return minInclusive + (int)(value % range);
        }

        public bool Chance(float probability) {
            if (probability <= 0f)
                return false;

            if (probability >= 1f)
                return true;

            const int precision = 1_000_000;

            int threshold = Mathf.RoundToInt(
                probability * precision);

            return NextInt(0, precision) < threshold;
        }
    }
}
