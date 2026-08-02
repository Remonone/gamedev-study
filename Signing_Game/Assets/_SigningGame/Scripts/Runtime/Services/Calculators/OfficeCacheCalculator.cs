using System;
using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Modifiers;
using Services.Locator;
using UnityEngine;

namespace Services.Calculators {
    public sealed class OfficeCacheCalculator : ICacheCalculator<OfficeEntries>, IService, IPreInitialize {
        public const int MaximumClerkCapacity = 256;
        public const float MaximumDocumentsPerSecond = 1000f;

        private IModifierService _modifierService;
        private IAssetProvider _assetProvider;
        private IAssetListLease<OfficeReference> _referenceLease;
        private OfficeReference _reference;

        public OfficeCacheCalculator() { }

        internal OfficeCacheCalculator(IAssetProvider assetProvider) {
            _assetProvider = assetProvider ?? throw new ArgumentNullException(nameof(assetProvider));
        }

        public OfficeEntries Calculate() {
            if (_reference == null) {
                throw new InvalidOperationException("Office configuration is not initialized.");
            }

            OfficeEntries effective = _modifierService.Apply(_reference.Value);
            return NormalizeEffective(effective);
        }

        public async UniTask PreInitializeAsync(IServiceScope scope) {
            _modifierService = scope.Get<IModifierService>();
            _assetProvider ??= scope.Container.Get<IAssetProvider>();
            _referenceLease = await _assetProvider.LoadAssetsByLabelAsync<OfficeReference>(
                AddressableConstants.CACHE_REFERENCE_LABEL);

            if (_referenceLease?.Assets == null || _referenceLease.Assets.Count != 1 ||
                _referenceLease.Assets[0] == null) {
                throw new InvalidOperationException(
                    "Exactly one non-null OfficeReference must have the 'cache_reference' Addressables label.");
            }

            _reference = _referenceLease.Assets[0];
            ValidateBase(_reference.Value);
        }

        public void Dispose() {
            _referenceLease?.Dispose();
            _referenceLease = null;
            _reference = null;
        }

        internal static void ValidateBase(OfficeEntries value) {
            if (value.ClerkCapacity < 0 || value.ClerkCapacity > MaximumClerkCapacity) {
                throw new InvalidOperationException(
                    $"Office clerk capacity must be between 0 and {MaximumClerkCapacity}.");
            }

            if (!IsFiniteInRange(value.DocumentsPerSecondPerClerk, 0f, MaximumDocumentsPerSecond)) {
                throw new InvalidOperationException(
                    $"Office processing speed must be finite and between 0 and {MaximumDocumentsPerSecond}.");
            }

            if (!IsFiniteInRange(value.QualityCeiling, 0f, 1f) ||
                !IsFiniteInRange(value.AcceptanceThreshold, 0f, 1f) ||
                !IsFiniteInRange(value.RewardMultiplier, 0f, 1f)) {
                throw new InvalidOperationException(
                    "Office quality, acceptance threshold, and reward multiplier must be finite values between 0 and 1.");
            }

            double baseCost = value.BaseHireCost.ToDouble();
            if (value.BaseHireCost.IsZero || double.IsNaN(baseCost) || double.IsInfinity(baseCost) || baseCost <= 0d) {
                throw new InvalidOperationException("Office base hire cost must be positive and finite.");
            }

            if (float.IsNaN(value.HireCostGrowthMultiplier) || float.IsInfinity(value.HireCostGrowthMultiplier) ||
                value.HireCostGrowthMultiplier < 1f) {
                throw new InvalidOperationException("Office hire cost growth multiplier must be finite and at least 1.");
            }
        }

        internal static OfficeEntries NormalizeEffective(OfficeEntries value) {
            bool normalized = false;

            int capacity = Math.Clamp(value.ClerkCapacity, 0, MaximumClerkCapacity);
            normalized |= capacity != value.ClerkCapacity;
            value.ClerkCapacity = capacity;

            value.DocumentsPerSecondPerClerk = Normalize(value.DocumentsPerSecondPerClerk, 0f,
                MaximumDocumentsPerSecond, 0f, ref normalized);
            value.QualityCeiling = Normalize(value.QualityCeiling, 0f, 1f, 0f, ref normalized);
            value.AcceptanceThreshold = Normalize(value.AcceptanceThreshold, 0f, 1f, 1f, ref normalized);
            value.RewardMultiplier = Normalize(value.RewardMultiplier, 0f, 1f, 0f, ref normalized);

            if (normalized) {
                Debug.LogWarning("Invalid effective office values were normalized to safe ranges.");
            }

            return value;
        }

        private static float Normalize(float value, float minimum, float maximum, float nonFiniteFallback,
            ref bool normalized) {
            if (float.IsNaN(value) || float.IsInfinity(value)) {
                normalized = true;
                return nonFiniteFallback;
            }

            float clamped = Math.Clamp(value, minimum, maximum);
            normalized |= !clamped.Equals(value);
            return clamped;
        }

        private static bool IsFiniteInRange(float value, float minimum, float maximum) {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum;
        }
    }
}
