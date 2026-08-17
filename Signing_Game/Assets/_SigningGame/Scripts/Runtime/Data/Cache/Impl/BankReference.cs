using System;
using UnityEngine;
using Utils;
using Utils.Attributes;

namespace Data.Cache {
    [Serializable, CacheEntryGroup("Bank")]
    public struct BankEntries {
        [ModifiableParameter("PayoutAmount", Minimum = 0d, Maximum = double.MaxValue)]
        public Value PayoutAmount;

        [ModifiableParameter("PayoutIntervalSeconds", Minimum = float.Epsilon, Maximum = float.MaxValue)]
        public float PayoutIntervalSeconds;

        [ModifiableParameter("CriticalChance", Minimum = 0d, Maximum = 1d)]
        public float CriticalChance;

        [ModifiableParameter("CriticalMultiplier", Minimum = 1d, Maximum = double.MaxValue)]
        public double CriticalMultiplier;

        [ModifiableParameter("BillCostCompensationRatio", Minimum = 0d, Maximum = 1d)]
        public double BillCostCompensationRatio;

        [ModifiableParameter("MultiPayChance", Minimum = 0d, Maximum = 100d)]
        public float MultiPayChance;
    }

    [CreateAssetMenu(menuName = "References/Bank Reference", fileName = "Bank Reference")]
    public sealed class BankReference : BaseEntries<BankEntries> { }
}
