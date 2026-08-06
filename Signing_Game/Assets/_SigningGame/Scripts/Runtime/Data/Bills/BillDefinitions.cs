using System;
using UnityEngine;

namespace Data.Bills {
    public enum BillRequirementKind {
        OwnedUpgrade = 0,
        MinimumClerkCount = 1,
        MinimumUnlockedDocumentQuality = 2
    }

    [Serializable]
    public struct BillRequirementBalance {
        [Min(1f)] public double CostMultiplier;
        [Min(0f)] public double WorkFactor;
        [Min(0f)] public double RewardFactor;
        [Range(0f, 1f)] public double DifficultyFactor;

        public static BillRequirementBalance Lerp(
            BillRequirementBalance minimum,
            BillRequirementBalance maximum,
            double t) {
            t = Math.Clamp(t, 0d, 1d);
            return new BillRequirementBalance {
                CostMultiplier = Lerp(minimum.CostMultiplier, maximum.CostMultiplier, t),
                WorkFactor = Lerp(minimum.WorkFactor, maximum.WorkFactor, t),
                RewardFactor = Lerp(minimum.RewardFactor, maximum.RewardFactor, t),
                DifficultyFactor = Lerp(minimum.DifficultyFactor, maximum.DifficultyFactor, t)
            };
        }

        private static double Lerp(double from, double to, double t) {
            return from + (to - from) * t;
        }
    }

}
