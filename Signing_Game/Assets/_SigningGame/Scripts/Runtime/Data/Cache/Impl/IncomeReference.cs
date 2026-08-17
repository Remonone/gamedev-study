using System;
using UnityEngine;
using Utils;
using Utils.Attributes;

namespace Data.Cache {
    [Serializable, CacheEntryGroup("Income")]
    public struct IncomeEntries {
        [ModifiableParameter("MaxMultiplicationScale", Minimum = 0d)]
        public float MaxMultiplicationScale;
        [ModifiableParameter("MinMultiplyScale", Minimum = float.Epsilon)]
        public float MinMultiplyScale;
        [ModifiableParameter("IncomePerDocument", Minimum = 0d)]
        public Value IncomePerDocument;

        [ModifiableParameter("ManualSignatureCriticalChance", Minimum = 0d, Maximum = 1d)]
        public float ManualSignatureCriticalChance;

        [ModifiableParameter("ManualSignatureCriticalMultiplier", Minimum = 1d)]
        public double ManualSignatureCriticalMultiplier;

        [ModifiableParameter("ManualSignatureMultiPayChance", Minimum = 0d, Maximum = 100d)]
        public float ManualSignatureMultiPayChance;

        public IncomeEntries(float maxMultiplicationScale, float minMultiplyScale, Value incomePerDocument)
            : this(maxMultiplicationScale, minMultiplyScale, incomePerDocument, 0f, 1d) { }

        public IncomeEntries(float maxMultiplicationScale, float minMultiplyScale, Value incomePerDocument,
            float manualSignatureCriticalChance, double manualSignatureCriticalMultiplier)
            : this(maxMultiplicationScale, minMultiplyScale, incomePerDocument,
                manualSignatureCriticalChance, manualSignatureCriticalMultiplier, 0f) { }

        public IncomeEntries(float maxMultiplicationScale, float minMultiplyScale, Value incomePerDocument,
            float manualSignatureCriticalChance, double manualSignatureCriticalMultiplier,
            float manualSignatureMultiPayChance) {
            MaxMultiplicationScale = maxMultiplicationScale;
            MinMultiplyScale = minMultiplyScale;
            IncomePerDocument = incomePerDocument;
            ManualSignatureCriticalChance = manualSignatureCriticalChance;
            ManualSignatureCriticalMultiplier = manualSignatureCriticalMultiplier;
            ManualSignatureMultiPayChance = manualSignatureMultiPayChance;
        }
    }
    
    [CreateAssetMenu(menuName = "References/Income Reference")]
    public class IncomeReference : BaseEntries<IncomeEntries> { }
}
