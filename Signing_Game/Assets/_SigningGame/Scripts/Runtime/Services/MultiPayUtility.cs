using System;

namespace Services {
    /// <summary>
    /// Shared math for the multi-pay income mechanic. A multi-pay chance above 100%
    /// guarantees additional payments: a chance of (100*n + k)% always yields n extra
    /// payments on top of the base one and a k% chance of one more payment.
    /// The chance is stored as a fraction, e.g. 1.24f equals 124%.
    /// </summary>
    public static class MultiPayUtility {
        public const float MaximumChance = 100f;

        /// <summary>
        /// Splits a multi-pay chance into the guaranteed amount of extra payments
        /// and the fractional remainder that acts as the chance for one more payment.
        /// </summary>
        public static int SplitChance(float chance, out float remainder) {
            if (float.IsNaN(chance) || chance <= 0f) {
                remainder = 0f;
                return 0;
            }

            if (chance >= MaximumChance) {
                remainder = 0f;
                return (int)MaximumChance;
            }

            int guaranteed = (int)Math.Floor(chance);
            remainder = chance - guaranteed;
            return guaranteed;
        }
    }
}
