using System;
using UnityEngine;
using Utils.Attributes;

namespace Data.Cache {
    [Serializable, CacheEntryGroup("Office")]
    public struct OfficeEntries {
        [ModifiableParameter("ClerkCapacity", Minimum = 0d, Maximum = 256d)]
        public int ClerkCapacity;

        [ModifiableParameter("DocumentsPerSecondPerClerk", Minimum = 0d, Maximum = 1000d)]
        public float DocumentsPerSecondPerClerk;

        [ModifiableParameter("QualityCeiling", Minimum = 0d, Maximum = 1d)]
        public float QualityCeiling;

        [ModifiableParameter("AcceptanceThreshold", Minimum = 0d, Maximum = 1d)]
        public float AcceptanceThreshold;

        [ModifiableParameter("RewardMultiplier", Minimum = 0d, Maximum = 1d)]
        public float RewardMultiplier;

        [ModifiableParameter("OfficeSignatureCriticalChance", Minimum = 0d, Maximum = 1d)]
        public float OfficeSignatureCriticalChance;

        [ModifiableParameter("OfficeSignatureCriticalMultiplier", Minimum = 1d)]
        public double OfficeSignatureCriticalMultiplier;

        [ModifiableParameter("BaseClerkMultiplierMedian", Minimum = double.Epsilon)]
        public double BaseClerkMultiplierMedian;

        [ModifiableParameter("ClerkMultiplierRangeStep", Minimum = 0d)]
        public double ClerkMultiplierRangeStep;

        [ModifiableParameter("MinimumClerkMultiplier", Minimum = 0d)]
        public double MinimumClerkMultiplier;

        [ModifiableParameter("MaximumHireSignatureMultiplier", Minimum = 1d)]
        public double MaximumHireSignatureMultiplier;

        [ModifiableParameter("SalaryReviewCostRatio", Minimum = 0d, Maximum = 1d)]
        public double SalaryReviewCostRatio;
    }

    [CreateAssetMenu(menuName = "References/Office Reference", fileName = "Office Reference")]
    public sealed class OfficeReference : BaseEntries<OfficeEntries> { }
}
