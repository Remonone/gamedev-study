using Data.Cache;

namespace Services.Calculators {
    public sealed class IncomeCacheCalculator : ICacheCalculator<IncomeEntries>, IService {
        
        public IncomeEntries Calculate() {
            return new IncomeEntries(1f, 0.4f, 1);
        }

        public void Dispose() { }
    }
}