using Utils;
using Utils.Attributes;

namespace Data.Cache {
    [CacheEntryGroup("Income")]
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
}