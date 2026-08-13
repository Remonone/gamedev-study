using Authoring;
using Data.Rules;
using Exceptions;
using UnityEngine;

namespace Services {
    public sealed class SelectedSignatureLoader : MonoBehaviour, IService {
        [SerializeField] private SignaturePresetDefinition _signaturePreset;

        private SignatureDifficultyRules _baseDifficulty;

        public SignaturePresetDefinition GetActivePreset() {
            if (_signaturePreset == null) {
                throw new SignaturePresetConfigurationException(
                    "SelectedSignatureLoader requires a signature preset reference.");
            }

            return _signaturePreset;
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
            _baseDifficulty = null;
        }
    }
}
