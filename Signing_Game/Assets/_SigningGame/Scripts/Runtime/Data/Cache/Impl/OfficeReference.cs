using System;
using UnityEngine;
using Utils;
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

        public Value BaseHireCost;
        public float HireCostGrowthMultiplier;
    }

    [CreateAssetMenu(menuName = "References/Office Reference", fileName = "Office Reference")]
    public sealed class OfficeReference : BaseEntries<OfficeEntries> { }
}
