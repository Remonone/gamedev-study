using System;
using UnityEngine;
using Utils.Attributes;

namespace Data.Cache {
    [Serializable, CacheEntryGroup("Bills")]
    public struct BillEntries {
        [ModifiableParameter("CatalogSize", Minimum = 1d, Maximum = 64d)]
        public int CatalogSize;

        [ModifiableParameter("ActiveProjectLimit", Minimum = 1d, Maximum = 64d)]
        public int ActiveProjectLimit;

        [ModifiableParameter("CostMultiplier", Minimum = float.Epsilon)]
        public float CostMultiplier;

        [ModifiableParameter("OverallRewardMultiplier", Minimum = 0d)]
        public float OverallRewardMultiplier;

        [ModifiableParameter("ActiveGenerationBonusMultiplier", Minimum = 0d)]
        public float ActiveGenerationBonusMultiplier;

        [ModifiableParameter("ActiveIncomePenaltyStrength", Minimum = 0d, Maximum = 1d)]
        public float ActiveIncomePenaltyStrength;

        [ModifiableParameter("MaximumSignatureRewardMultiplier", Minimum = 1d)]
        public float MaximumSignatureRewardMultiplier;

        [ModifiableParameter("RequirementRewardFactorMultiplier", Minimum = 0d)]
        public float RequirementRewardFactorMultiplier;

        public float BaseSignatureThreshold;
        public float MaximumThresholdAddition;
        public float BaseActiveIncomeMultiplier;
        public int MaximumPriorityWeight;
    }

    [CreateAssetMenu(menuName = "References/Bill Reference", fileName = "Bill Reference")]
    public sealed class BillReference : BaseEntries<BillEntries> { }
}
