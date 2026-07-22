using Authoring;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Rules;
using Services.Locator;
using UnityEngine.AddressableAssets;

namespace Services {
    public class DifficultyProfileEvaluator : IService, IInitialize {

        private IAssetLease<SignatureDifficultyProfileDefinition> _difficultyProfile;
        
        private SignatureDifficultyRules _rules;
        private bool _isDirty = true;
        
        public SignatureDifficultyRules GetDifficultyProfile() {
            if (!_isDirty) return _rules;

            _rules = _difficultyProfile.Asset.ToRules();
            _isDirty = false;
            return _rules;
        }
        
        public void Dispose() {
            _difficultyProfile?.Dispose();
        }

        public async UniTask InitializeAsync(IServiceScope scope) {
            var assetProvider = scope.Container.Get<IAssetProvider>();
            var profileLease = await assetProvider.LoadAsync(new AssetReferenceT<SignatureDifficultyProfileDefinition>("Assets/_SigningGame/Data/Difficulty.asset"));
            _difficultyProfile = profileLease;
        }
    }
}