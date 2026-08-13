using Authoring;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Rules;
using Services.Locator;

namespace Services {
    public class PlayerStatStash : IService, IPreInitialize, IInitialize {

        private SelectedSignatureLoader _signatureLoader;
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

        public float GetIncomeModifiers() {
            return 1;
        }

        public SignaturePresetDefinition GetActivePreset() => _signatureLoader.GetActivePreset();

        public SignatureDifficultyRules GetConfiguredSignatureDifficulty() => _signatureLoader.GetBaseDifficulty();

        public SignatureDifficultyRules GetEffectiveSignatureDifficulty() {
            return _signatureData.Value.ToRules(GetConfiguredSignatureDifficulty().Id);
        }
        
        public UniTask InitializeAsync(IServiceScope scope) {
            _signatureLoader = scope.Get<SelectedSignatureLoader>();
            return UniTask.CompletedTask;
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
