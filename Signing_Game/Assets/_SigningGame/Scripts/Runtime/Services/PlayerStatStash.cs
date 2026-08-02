using Authoring;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Rules;
using Services.Locator;

namespace Services {
    public class PlayerStatStash : IService, IPreInitialize, IInitialize, IPostInitialize {

        private ISignaturePresetRepository _signaturePresetRepository;
        
        private SignaturePresetDefinition _signaturePreset;
        private ICacheDataFactory _dataFactory;

        private CachedData<IncomeEntries> _incomeData;
        private CachedData<SignatureEntries> _signatureData;
        private CachedData<GenerationEntries> _generationData;
        private CachedData<OfficeEntries> _officeData;
        
        public IReadOnlyCacheData<IncomeEntries> IncomeData => _incomeData;
        public IReadOnlyCacheData<SignatureEntries> SignatureData => _signatureData;
        public IReadOnlyCacheData<GenerationEntries> GenerationData => _generationData;
        public IReadOnlyCacheData<OfficeEntries> OfficeData => _officeData;
        
        
        public void Dispose() {
        }

        public SignatureRuleModifiers GetSignatureModifiers() {
            return SignatureRuleModifiers.None;
        }

        public float GetIncomeModifiers() {
            return 1;
        }

        public SignaturePresetDefinition GetActivePreset() => _signaturePreset;
        
        public UniTask InitializeAsync(IServiceScope scope) {
            _signaturePresetRepository = scope.Get<ISignaturePresetRepository>();
            
            
            return UniTask.CompletedTask;
        }

        public async UniTask PostInitializeAsync(IServiceScope scope) {
            _signaturePreset = await _signaturePresetRepository.RequestPreset(_signatureData.Value.SignatureId);
        }

        public UniTask PreInitializeAsync(IServiceScope scope) {
            _dataFactory = new CachedDataFactory(scope, scope.Get<ICacheVersionProvider>());
            _incomeData = _dataFactory.Create<IncomeEntries>();
            _signatureData = _dataFactory.Create<SignatureEntries>();
            _generationData = _dataFactory.Create<GenerationEntries>();
            _officeData = _dataFactory.Create<OfficeEntries>();
            return UniTask.CompletedTask;
        }
    }
}
