using Data.Cache;

namespace Services.Calculators {
    public sealed class SignatureCacheCalculator : ICacheCalculator<SignatureEntries>, IService {
        public void Dispose() { }

        public SignatureEntries Calculate() {
            return new SignatureEntries("test_preset");
        }
    }
}