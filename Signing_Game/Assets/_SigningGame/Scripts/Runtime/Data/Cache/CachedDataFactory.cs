using Services.Locator;

namespace Data.Cache {
    public sealed class CachedDataFactory : ICacheDataFactory {

        private readonly IServiceScope _scope;
        private readonly ICacheVersionProvider _versionProvider;
        
        public CachedDataFactory(IServiceScope scope, ICacheVersionProvider versionProvider) {
            _scope = scope;
            _versionProvider = versionProvider;
        }
        
        public CachedData<T> Create<T>() {
            var calculator = _scope.Get<ICacheCalculator<T>>();
            return new CachedData<T>(_versionProvider, calculator);
        }
    }
}