using Utils;

namespace Data.Cache {
    public sealed record IncomeEntries(float MaxMultiplicationScale, float MinMultiplyScale, Value IncomePerDocument);
}