using System;

namespace Services {
    internal interface IBillRandom {
        ulong State { get; }
        int NextInt(int minimumInclusive, int maximumExclusive);
        bool Chance(double probability);
        IBillRandom Fork();
    }

    internal sealed class BillRandom : IBillRandom {
        private ulong _state;

        public ulong State => _state;

        public BillRandom(ulong state) {
            _state = state;
        }

        public int NextInt(int minimumInclusive, int maximumExclusive) {
            if (maximumExclusive <= minimumInclusive) {
                throw new ArgumentOutOfRangeException(nameof(maximumExclusive));
            }

            ulong range = (ulong)((long)maximumExclusive - minimumInclusive);
            ulong threshold = unchecked((0UL - range) % range);
            ulong value;
            do {
                value = NextUInt64();
            } while (value < threshold);
            return minimumInclusive + (int)(value % range);
        }

        public bool Chance(double probability) {
            if (probability <= 0d) return false;
            if (probability >= 1d) return true;
            const int precision = 1_000_000;
            int threshold = (int)Math.Round(probability * precision);
            return NextInt(0, precision) < threshold;
        }

        public IBillRandom Fork() {
            return new BillRandom(_state);
        }

        private ulong NextUInt64() {
            _state += 0x9E3779B97F4A7C15UL;
            ulong value = _state;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}
