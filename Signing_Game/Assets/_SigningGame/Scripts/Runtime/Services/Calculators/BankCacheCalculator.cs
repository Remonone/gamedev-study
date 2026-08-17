using System;
using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Modifiers;
using Services.Locator;
using UnityEngine;
using Utils;

namespace Services.Calculators {
    public sealed class BankCacheCalculator : ICacheCalculator<BankEntries>, IService, IPreInitialize {
        public static readonly Value MaximumPayoutAmount = new(double.MaxValue);

        public const float DefaultPayoutIntervalSeconds = 10f;
        public const double DefaultCriticalMultiplier = 2d;

        private IModifierService _modifierService;
        private IAssetProvider _assetProvider;
        private IAssetListLease<BankReference> _referenceLease;
        private BankReference _reference;

        public BankCacheCalculator() { }

        internal BankCacheCalculator(IAssetProvider assetProvider) {
            _assetProvider = assetProvider ?? throw new ArgumentNullException(nameof(assetProvider));
        }

        public BankEntries Calculate() {
            if (_reference == null) {
                throw new InvalidOperationException("Bank configuration is not initialized.");
            }

            BankEntries effective = _modifierService.Apply(_reference.Value);
            return NormalizeEffective(effective);
        }

        public async UniTask PreInitializeAsync(IServiceScope scope) {
            _modifierService = scope.Get<IModifierService>();
            _assetProvider ??= scope.Container.Get<IAssetProvider>();
            _referenceLease = await _assetProvider.LoadAssetsByLabelAsync<BankReference>(
                AddressableConstants.CACHE_REFERENCE_LABEL);

            if (_referenceLease?.Assets == null || _referenceLease.Assets.Count != 1 ||
                _referenceLease.Assets[0] == null) {
                throw new InvalidOperationException(
                    "Exactly one non-null BankReference must have the 'cache_reference' Addressables label.");
            }

            _reference = _referenceLease.Assets[0];
            ValidateBase(_reference.Value);
        }

        public void Dispose() {
            _referenceLease?.Dispose();
            _referenceLease = null;
            _reference = null;
        }

        internal static void ValidateBase(BankEntries value) {
            if (!IsCanonicalPayout(value.PayoutAmount) || value.PayoutAmount > MaximumPayoutAmount) {
                throw new InvalidOperationException(
                    "Bank payout amount must be canonical, non-negative, and no greater than double.MaxValue.");
            }

            if (!IsFiniteInRange(value.PayoutIntervalSeconds, float.Epsilon, float.MaxValue)) {
                throw new InvalidOperationException("Bank payout interval must be finite and positive.");
            }

            if (!IsFiniteInRange(value.CriticalChance, 0f, 1f)) {
                throw new InvalidOperationException("Bank critical chance must be finite and between 0 and 1.");
            }

            if (!IsFiniteInRange(value.CriticalMultiplier, 1d, double.MaxValue)) {
                throw new InvalidOperationException("Bank critical multiplier must be finite and at least 1.");
            }

            if (!IsFiniteInRange(value.BillCostCompensationRatio, 0d, 1d)) {
                throw new InvalidOperationException(
                    "Bank bill cost compensation ratio must be finite and between 0 and 1.");
            }

            if (!IsFiniteInRange(value.MultiPayChance, 0f, Services.MultiPayUtility.MaximumChance)) {
                throw new InvalidOperationException(
                    $"Bank multi-pay chance must be finite and between 0 and {Services.MultiPayUtility.MaximumChance}.");
            }
        }

        internal static BankEntries NormalizeEffective(BankEntries value) {
            bool normalized = false;

            if (!IsCanonicalPayout(value.PayoutAmount)) {
                value.PayoutAmount = Value.Zero;
                normalized = true;
            }
            else if (value.PayoutAmount > MaximumPayoutAmount) {
                value.PayoutAmount = MaximumPayoutAmount;
                normalized = true;
            }

            value.PayoutIntervalSeconds = Normalize(value.PayoutIntervalSeconds, float.Epsilon, float.MaxValue,
                DefaultPayoutIntervalSeconds, ref normalized);
            value.CriticalChance = Normalize(value.CriticalChance, 0f, 1f, 0f, ref normalized);
            value.CriticalMultiplier = Normalize(value.CriticalMultiplier, 1d, double.MaxValue,
                DefaultCriticalMultiplier, ref normalized);
            value.BillCostCompensationRatio = Normalize(value.BillCostCompensationRatio, 0d, 1d, 0d,
                ref normalized);
            value.MultiPayChance = Normalize(value.MultiPayChance, 0f, Services.MultiPayUtility.MaximumChance, 0f,
                ref normalized);

            if (normalized) Debug.LogWarning("Invalid effective bank values were normalized to safe ranges.");
            return value;
        }

        private static bool IsCanonicalPayout(Value value) {
            double stored = value.Stored;
            int degree = value.Base.Degree;
            if (double.IsNaN(stored) || double.IsInfinity(stored) || stored < 0d || stored >= 1000d || degree < 0) {
                return false;
            }

            if (stored == 0d) return degree == 0;
            return degree == 0 || stored >= 1d;
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

        private static bool IsFiniteInRange(float value, float minimum, float maximum) {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum;
        }

        private static bool IsFiniteInRange(double value, double minimum, double maximum) {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= minimum && value <= maximum;
        }
    }
}
