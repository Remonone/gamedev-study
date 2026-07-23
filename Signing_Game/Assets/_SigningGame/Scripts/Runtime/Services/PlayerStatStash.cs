using Authoring;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Rules;
using Services.Locator;
using Utils;

namespace Services {
    public class PlayerStatStash : IService, IInitialize, IPostInitialize {

        private ISignaturePresetRepository _signaturePresetRepository;
        
        private string _signatureId = "test_preset";
        private SignaturePresetDefinition _signaturePreset;
        // TODO: Change to PropertyHolder
        private double _maxMultiplicationScale = 1;
        private float _minMultiplyScale = 0.4f;
        private Value _incomePerDocument = 1;
        private float _generationTokenPerSecond = 1;
        private float _dispenseCooldown = 5f;
            
        public double MaxMultiplicationScale => _maxMultiplicationScale;
        public float MinMultiplyScale => _minMultiplyScale;
        public Value IncomePerDocument => _incomePerDocument;
        public float GenerationTokenPerSecond => _generationTokenPerSecond;
        public float DispenseCooldown => _dispenseCooldown;
        
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
            _signaturePreset = await _signaturePresetRepository.RequestPreset(_signatureId);
        }
    }
}