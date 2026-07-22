using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Authoring;
using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Templates;
using Exceptions;
using Services.Locator;

namespace Services {
    public sealed class SignaturePresetRepository : IService, ISignaturePresetRepository, IPreInitialize, IInitialize {
        
        private ISignaturePresetCompiler _compiler;
        private IAssetListLease<SignaturePresetDefinition> _presetLease;
        private readonly Dictionary<string, SignaturePresetDefinition> _presetsById = new(StringComparer.Ordinal);
        private readonly Dictionary<SignaturePresetDefinition, CompiledSignaturePreset> _cache =
            new(ReferenceIdentityComparer.Instance);
        private bool _initialized;
        
        public async UniTask PreInitializeAsync(IServiceScope scope) {
            IAssetProvider provider = scope.Container.Get<IAssetProvider>();
            _presetLease = await provider.LoadAssetsByLabelAsync<SignaturePresetDefinition>(
                AddressableConstants.SIGNATURE_PRESET_LABEL);

            _presetsById.Clear();
            foreach (SignaturePresetDefinition preset in _presetLease.Assets) {
                RegisterPreset(preset);
            }
        }

        public UniTask InitializeAsync(IServiceScope scope) {
            ISignaturePresetCompiler compiler = scope.Get<ISignaturePresetCompiler>();
            _compiler = compiler;
              
            _initialized = true;

            return UniTask.CompletedTask;
        }

        public UniTask<SignaturePresetDefinition> RequestPreset(string id) {
            if (!_initialized)
                throw new InvalidOperationException("SignaturePresetRepository must be initialized before use.");
            if (_presetLease == null)
                throw new InvalidOperationException("SignaturePresetRepository must be pre-initialized before requesting presets.");
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Preset id cannot be empty.", nameof(id));
            if (!_presetsById.TryGetValue(id, out SignaturePresetDefinition preset)) {
                throw new KeyNotFoundException(
                    $"Signature preset '{id}' is not loaded. Check Addressables label '{AddressableConstants.SIGNATURE_PRESET_LABEL}'.");
            }

            return UniTask.FromResult(preset);
        }

        public CompiledSignaturePreset GetOrCompile(SignaturePresetDefinition preset) {
            if (!_initialized)
                throw new InvalidOperationException("SignaturePresetRepository must be initialized before use.");
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            if (_cache.TryGetValue(preset, out CompiledSignaturePreset compiled)) return compiled;
            compiled = _compiler.Compile(preset);
            _cache.Add(preset, compiled);
            return compiled;
        }

        public void Invalidate(SignaturePresetDefinition preset) {
            if (preset == null) return;
            _cache.Remove(preset);
        }

        public void InvalidateById(string presetId) {
            var removed = new List<SignaturePresetDefinition>();
            foreach (SignaturePresetDefinition preset in _cache.Keys)
                if (string.Equals(preset.Id, presetId, StringComparison.Ordinal)) removed.Add(preset);
            foreach (SignaturePresetDefinition preset in removed) _cache.Remove(preset);
        }

        public void Clear() => _cache.Clear();

        public void Dispose() {
            Clear();
            _presetLease?.Dispose();
            _presetLease = null;
            _presetsById.Clear();
        }

        private void RegisterPreset(SignaturePresetDefinition preset) {
            if (preset == null) {
                throw new SignaturePresetConfigurationException(
                    $"Addressables label '{AddressableConstants.SIGNATURE_PRESET_LABEL}' contains a null signature preset.");
            }

            if (string.IsNullOrWhiteSpace(preset.Id)) {
                throw new SignaturePresetConfigurationException(
                    $"Signature preset '{preset.name}' has empty Id.");
            }

            if (!_presetsById.TryAdd(preset.Id, preset)) {
                throw new SignaturePresetConfigurationException(
                    $"Duplicate signature preset Id '{preset.Id}' in Addressables label '{AddressableConstants.SIGNATURE_PRESET_LABEL}'.");
            }
        }

        private sealed class ReferenceIdentityComparer : IEqualityComparer<SignaturePresetDefinition> {
            public static readonly ReferenceIdentityComparer Instance = new();
            public bool Equals(SignaturePresetDefinition x, SignaturePresetDefinition y) => ReferenceEquals(x, y);
            public int GetHashCode(SignaturePresetDefinition obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
