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
        public const double DefaultBaseClerkMultiplierMedian = 2d;
        public const double DefaultClerkMultiplierRangeStep = 1d;
        public const double DefaultMinimumClerkMultiplier = 1d;
        public const double DefaultMaximumHireSignatureMultiplier = 2d;
        public const double DefaultSalaryReviewCostRatio = 0.5d;
        public const double DefaultOfficeSignatureCriticalMultiplier = 1d;

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
                !IsFiniteInRange(value.RewardMultiplier, 0f, 1f) ||
                !IsFiniteInRange(value.OfficeSignatureCriticalChance, 0f, 1f)) {
                throw new InvalidOperationException(
                    "Office quality, acceptance threshold, reward multiplier, and critical chance must be finite values between 0 and 1.");
            }

            if (!IsFiniteInRange(value.BaseClerkMultiplierMedian, double.Epsilon, double.MaxValue)) {
                throw new InvalidOperationException("Office base clerk multiplier median must be positive and finite.");
            }

            if (!IsFiniteInRange(value.ClerkMultiplierRangeStep, 0d, double.MaxValue) ||
                !IsFiniteInRange(value.MinimumClerkMultiplier, 0d, double.MaxValue) ||
                !IsFiniteInRange(value.OfficeSignatureCriticalMultiplier, 1d, double.MaxValue) ||
                !IsFiniteInRange(value.MaximumHireSignatureMultiplier, 1d, double.MaxValue) ||
                !IsFiniteInRange(value.SalaryReviewCostRatio, 0d, 1d)) {
                throw new InvalidOperationException(
                    "Office clerk multiplier range step and minimum must be finite and non-negative, critical and maximum hire signature multipliers must be finite and at least 1, and the salary review cost ratio must be between 0 and 1.");
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
            value.OfficeSignatureCriticalChance = Normalize(value.OfficeSignatureCriticalChance, 0f, 1f, 0f,
                ref normalized);
            value.OfficeSignatureCriticalMultiplier = Normalize(value.OfficeSignatureCriticalMultiplier, 1d,
                double.MaxValue, DefaultOfficeSignatureCriticalMultiplier, ref normalized);
            value.BaseClerkMultiplierMedian = Normalize(value.BaseClerkMultiplierMedian, double.Epsilon,
                double.MaxValue, DefaultBaseClerkMultiplierMedian, ref normalized);
            value.ClerkMultiplierRangeStep = Normalize(value.ClerkMultiplierRangeStep, 0d, double.MaxValue,
                DefaultClerkMultiplierRangeStep, ref normalized);
            value.MinimumClerkMultiplier = Normalize(value.MinimumClerkMultiplier, 0d, double.MaxValue,
                DefaultMinimumClerkMultiplier, ref normalized);
            value.MaximumHireSignatureMultiplier = Normalize(value.MaximumHireSignatureMultiplier, 1d,
                double.MaxValue, DefaultMaximumHireSignatureMultiplier, ref normalized);
            value.SalaryReviewCostRatio = Normalize(value.SalaryReviewCostRatio, 0d, 1d,
                DefaultSalaryReviewCostRatio, ref normalized);

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

        private static double Normalize(double value, double minimum, double maximum, double nonFiniteFallback,
            ref bool normalized) {
            if (double.IsNaN(value) || double.IsInfinity(value)) {
                normalized = true;
                return nonFiniteFallback;
            }

            double clamped = Math.Clamp(value, minimum, maximum);
            normalized |= !clamped.Equals(value);
            return clamped;
        }

        private static bool IsFiniteInRange(double value, double minimum, double maximum) {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= minimum && value <= maximum;
        }
    }
}
