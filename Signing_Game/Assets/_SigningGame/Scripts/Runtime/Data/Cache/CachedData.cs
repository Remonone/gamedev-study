using System;

namespace Data.Cache {
    public class CachedData<T> : IReadOnlyCacheData<T> {
        private readonly ICacheVersionProvider _versionProvider;
        private readonly ICacheCalculator<T> _calculator;

        private int _calculatedVersion = -1;
        private T _value;
        
        public CachedData(ICacheVersionProvider provider, ICacheCalculator<T> calculator) {
            _versionProvider = provider;
            _calculator = calculator;
        }

        public T Value {
            get {
                var currentVersion = _versionProvider.GetVersion<T>();
                if (_calculatedVersion != currentVersion) {
                    _value = _calculator.Calculate();
                    _calculatedVersion = currentVersion;
                }
                return _value;
            }
        }
    }
}