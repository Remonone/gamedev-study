using System;

namespace Utils.Text.Generator {
    public static class SeedUtility {
        public static ulong Derive(ulong rootSeed, ulong streamId) {
            ulong value = rootSeed ^ (streamId + 0x9E3779B97F4A7C15UL);

            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;

            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;

            return value ^ (value >> 31);
        }

        public static ulong FromString(string value) {
            if (value == null) throw new ArgumentNullException(nameof(value));

            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;

            ulong hash = offset;

            foreach (char character in value) {
                hash ^= character;
                hash *= prime;
            }

            return hash;
        }
    }
}