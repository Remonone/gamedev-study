using Data.Cache;

namespace Services.Calculators {
    public sealed class GenerationCacheCalculator : ICacheCalculator<GenerationEntries>, IService {
        
        public void Dispose() { }

        public GenerationEntries Calculate() {
            return new GenerationEntries(1, 11f);
        }
    }
}