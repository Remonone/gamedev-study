using System;
using Utils;
using UnityEngine;

namespace Data.Bills {
    public enum BillRequirementKind {
        OwnedUpgrade = 0,
        MinimumClerkCount = 1,
        MinimumUnlockedDocumentQuality = 2,
        MinimumIncome = 3,
        ProcessedDocuments = 4
    }

    [Serializable]
    public abstract class BillRequirementDefinition {
        public abstract BillRequirementKind Kind { get; }
    }

    [Serializable]
    public sealed class OwnedUpgradeRequirementDefinition : BillRequirementDefinition {
        public string UpgradeId;
        public override BillRequirementKind Kind => BillRequirementKind.OwnedUpgrade;
    }

    [Serializable]
    public sealed class MinimumClerkCountRequirementDefinition : BillRequirementDefinition {
        public int MinimumTarget = 2;
        public int MaximumTarget = 10;
        public override BillRequirementKind Kind => BillRequirementKind.MinimumClerkCount;
    }

    [Serializable]
    public sealed class MinimumDocumentQualityRequirementDefinition : BillRequirementDefinition {
        public int MinimumTarget = 2;
        public int MaximumTarget = 9;
        public override BillRequirementKind Kind => BillRequirementKind.MinimumUnlockedDocumentQuality;
    }

    [Serializable]
    public sealed class MinimumIncomeRequirementDefinition : BillRequirementDefinition {
        public Value MinimumTarget = Value.One;
        public Value MaximumTarget = new Value(1000d);
        public override BillRequirementKind Kind => BillRequirementKind.MinimumIncome;
    }

    [Serializable]
    public sealed class ProcessedDocumentsRequirementDefinition : BillRequirementDefinition {
        public int MinimumTarget = 10;
        public int MaximumTarget = 1000;
        public override BillRequirementKind Kind => BillRequirementKind.ProcessedDocuments;
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
