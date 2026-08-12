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
        private CachedData<BankEntries> _bankData;
        private CachedData<DocumentEntries> _documents;
        private CachedData<BillEntries> _bills;
        private CachedData<ResearchEntries> _research;
        
        public IReadOnlyCacheData<IncomeEntries> IncomeData => _incomeData;
        public IReadOnlyCacheData<SignatureEntries> SignatureData => _signatureData;
        public IReadOnlyCacheData<GenerationEntries> GenerationData => _generationData;
        public IReadOnlyCacheData<OfficeEntries> OfficeData => _officeData;
        public IReadOnlyCacheData<BankEntries> BankData => _bankData;
        public IReadOnlyCacheData<DocumentEntries> Documents => _documents;
        public IReadOnlyCacheData<BillEntries> BillData => _bills;
        public IReadOnlyCacheData<ResearchEntries> ResearchData => _research;
        
        
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
            ICacheVersionProvider versionProvider = scope.Get<ICacheVersionProvider>();
            _dataFactory = new CachedDataFactory(scope, versionProvider);
            _incomeData = _dataFactory.Create<IncomeEntries>();
            _signatureData = _dataFactory.Create<SignatureEntries>();
            _generationData = _dataFactory.Create<GenerationEntries>();
            _officeData = _dataFactory.Create<OfficeEntries>();
            if (scope.TryGet(out ICacheCalculator<BankEntries> bankCalculator)) {
                _bankData = new CachedData<BankEntries>(versionProvider, bankCalculator);
            }
            _documents = _dataFactory.Create<DocumentEntries>();
            if (scope.TryGet(out ICacheCalculator<BillEntries> billCalculator)) {
                _bills = new CachedData<BillEntries>(versionProvider, billCalculator);
            }
            if (scope.TryGet(out ICacheCalculator<ResearchEntries> researchCalculator)) {
                _research = new CachedData<ResearchEntries>(versionProvider, researchCalculator);
            }
            return UniTask.CompletedTask;
        }
    }
}
