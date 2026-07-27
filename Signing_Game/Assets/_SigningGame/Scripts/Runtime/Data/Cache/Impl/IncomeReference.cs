using System;
using UnityEngine;
using Utils;
using Utils.Attributes;

namespace Data.Cache {
    [Serializable, CacheEntryGroup("Income")]
    public struct IncomeEntries {
        [ModifiableParameter("MaxMultiplicationScale")]
        public float MaxMultiplicationScale;
        [ModifiableParameter("MinMultiplyScale")]
        public float MinMultiplyScale;
        [ModifiableParameter("IncomePerDocument")]
        public Value IncomePerDocument;
        
        public IncomeEntries(float maxMultiplicationScale, float minMultiplyScale, Value incomePerDocument) {
            MaxMultiplicationScale = maxMultiplicationScale;
            MinMultiplyScale = minMultiplyScale;
            IncomePerDocument = incomePerDocument;
        }
    }
    
    [CreateAssetMenu(menuName = "References/Income Reference")]
    public class IncomeReference : BaseEntries<IncomeEntries> { }
}