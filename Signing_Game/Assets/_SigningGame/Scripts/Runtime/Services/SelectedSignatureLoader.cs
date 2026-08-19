using System;
using Authoring;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Rules;
using Exceptions;
using R3;
using Services.Locator;
using Utils;

namespace Services {
    public sealed class SelectedSignatureLoader : IService, IInitialize {
        private ISignaturePresetRepository _repository;
        private SignatureProgressionService _progression;
        private ICacheInvalidator _cacheInvalidator;
        private IDisposable _selectionSubscription;
        private SignatureDifficultyRules _baseDifficulty;
        private readonly SignaturePresetDefinition _fixedPreset;
        private readonly bool _usesFixedPreset;

        public SelectedSignatureLoader() { }

        internal SelectedSignatureLoader(SignaturePresetDefinition fixedPreset) {
            _fixedPreset = fixedPreset;
            _usesFixedPreset = true;
        }

        public UniTask InitializeAsync(IServiceScope scope) {
            _repository = scope.Get<ISignaturePresetRepository>();
            _progression = scope.Get<SignatureProgressionService>();
            _cacheInvalidator = scope.Get<ICacheInvalidator>();
            _selectionSubscription = _progression.ActivePresetChanged.Subscribe(_ => OnActivePresetChanged());
            return UniTask.CompletedTask;
        }

        public SignaturePresetDefinition GetActivePreset() {
            if (!TryGetActivePreset(out SignaturePresetDefinition preset)) {
                throw new SignaturePresetConfigurationException("An active signature must be selected before signing documents.");
            }
            return preset;
        }

        public bool TryGetActivePreset(out SignaturePresetDefinition preset) {
            if (_usesFixedPreset) {
                preset = _fixedPreset;
                return preset != null;
            }

            string activeId = _progression?.ActivePresetId;
            if (string.IsNullOrWhiteSpace(activeId) || _repository == null ||
                !_repository.TryGetPreset(activeId, out preset)) {
                preset = null;
                return false;
            }

            return true;
        }

        public bool TryGetBaseIncome(out Value income) {
            income = default;
            if (!TryGetActivePreset(out SignaturePresetDefinition preset) || preset.BaseIncome.IsZero) return false;
            income = preset.BaseIncome;
            return true;
        }

        public SignatureDifficultyRules GetBaseDifficulty() {
            if (_baseDifficulty != null) return _baseDifficulty;
            SignaturePresetDefinition preset = GetActivePreset();
            if (preset.BaseDifficultyProfile == null) {
                throw new SignaturePresetConfigurationException(
                    $"Signature preset '{preset.name}' requires a base difficulty profile.");
            }
            _baseDifficulty = preset.BaseDifficultyProfile.ToRules();
            return _baseDifficulty;
        }

        public void Dispose() {
            _selectionSubscription?.Dispose();
            _selectionSubscription = null;
            _baseDifficulty = null;
            _repository = null;
            _progression = null;
            _cacheInvalidator = null;
        }

        private void OnActivePresetChanged() {
            _baseDifficulty = null;
            _cacheInvalidator?.Invalidate<SignatureEntries>();
            _cacheInvalidator?.Invalidate<IncomeEntries>();
        }
    }
}
