using System;
using System.Collections.Generic;
using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Enums;
using Data.Formulas;
using Data.Modifiers;
using Data.Upgrades;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using R3;
using Services;
using Services.Calculators;
using Services.Locator;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;
using Utils;

namespace Tests.EditMode {
    public sealed class BankSystemTests {
        private readonly List<UnityEngine.Object> _objects = new();
        private readonly List<IDisposable> _disposables = new();

        [TearDown]
        public void TearDown() {
            for (int index = _disposables.Count - 1; index >= 0; index--) _disposables[index].Dispose();
            _disposables.Clear();
            for (int index = _objects.Count - 1; index >= 0; index--) {
                if (_objects[index] != null) UnityEngine.Object.DestroyImmediate(_objects[index]);
            }
            _objects.Clear();
        }

        [Test]
        public void LockedBank_PausesAndPreservesElapsedTime() {
            BankHarness harness = CreateHarness(DefaultEntries());
            harness.Bank.Deserialize(new JObject { ["elapsedSeconds"] = 5d });

            harness.Bank.Tick(20f);
            Assert.That(harness.Bank.ElapsedSeconds, Is.EqualTo(5d));
            Value balanceBeforeUnlock = harness.Wallet.CurrentBalance;

            harness.UnlockBank();
            Value balanceAfterUnlock = harness.Wallet.CurrentBalance;
            harness.Bank.Tick(5f);

            Assert.That(harness.Bank.ElapsedSeconds, Is.Zero);
            Assert.That(balanceAfterUnlock, Is.LessThan(balanceBeforeUnlock));
            Assert.That(harness.Wallet.CurrentBalance, Is.EqualTo(balanceAfterUnlock + Value.One));
        }

        [Test]
        public void Tick_UsesIndependentCriticalRollsAndKeepsFractionalProgress() {
            BankEntries entries = DefaultEntries();
            entries.PayoutAmount = new Value(2d);
            entries.CriticalChance = 0.5f;
            entries.CriticalMultiplier = 3d;
            float[] samples = { 0.1f, 0.9f };
            int sampleIndex = 0;
            BankHarness harness = CreateHarness(entries, () => samples[sampleIndex++]);
            harness.UnlockBank();
            Value before = harness.Wallet.CurrentBalance;

            harness.Bank.Tick(25f);

            Assert.That(sampleIndex, Is.EqualTo(2));
            Assert.That(harness.Wallet.CurrentBalance, Is.EqualTo(before + new Value(8d)));
            Assert.That(harness.Bank.ElapsedSeconds, Is.EqualTo(5d).Within(0.0001d));
        }

        [Test]
        public void CriticalChance_HandlesExactZeroAndOneEndpoints() {
            BankEntries entries = DefaultEntries();
            entries.PayoutAmount = new Value(2d);
            entries.CriticalChance = 0f;
            entries.CriticalMultiplier = 3d;
            BankHarness neverCritical = CreateHarness(entries, () => 0f);
            neverCritical.UnlockBank();
            Value neverCriticalBefore = neverCritical.Wallet.CurrentBalance;
            neverCritical.Bank.Tick(10f);
            Assert.That(neverCritical.Wallet.CurrentBalance,
                Is.EqualTo(neverCriticalBefore + new Value(2d)));

            entries.CriticalChance = 1f;
            BankHarness alwaysCritical = CreateHarness(entries, () => 1f);
            alwaysCritical.UnlockBank();
            Value alwaysCriticalBefore = alwaysCritical.Wallet.CurrentBalance;
            alwaysCritical.Bank.Tick(10f);
            Assert.That(alwaysCritical.Wallet.CurrentBalance,
                Is.EqualTo(alwaysCriticalBefore + new Value(6d)));
        }

        [Test]
        public void Tick_MultiPayChanceGrantsSeparatePaymentsWithIndependentCriticalRolls() {
            BankEntries entries = DefaultEntries();
            entries.PayoutAmount = new Value(2d);
            entries.CriticalChance = 0.5f;
            entries.CriticalMultiplier = 3d;
            entries.MultiPayChance = 1.24f;
            float[] samples = { 0.1f, 0.1f, 0.9f, 0.1f, 0.9f, 0.9f, 0.9f };
            int sampleIndex = 0;
            BankHarness harness = CreateHarness(entries, () => samples[sampleIndex++]);
            harness.UnlockBank();
            Value before = harness.Wallet.CurrentBalance;

            harness.Bank.Tick(25f);

            // Interval 1: multi-pay roll 0.1 < 0.24 -> 3 payments; crit rolls 0.1(crit), 0.9, 0.1(crit) -> 6 + 2 + 6.
            // Interval 2: multi-pay roll 0.9 >= 0.24 -> 2 payments; crit rolls 0.9, 0.9 -> 2 + 2.
            Assert.That(sampleIndex, Is.EqualTo(7));
            Assert.That(harness.Wallet.CurrentBalance, Is.EqualTo(before + new Value(18d)));
            Assert.That(harness.Bank.ElapsedSeconds, Is.EqualTo(5d).Within(0.0001d));
        }

        [Test]
        public void Tick_MultiPayChanceGuaranteedWholePartAlwaysPaysExtra() {
            BankEntries entries = DefaultEntries();
            entries.PayoutAmount = new Value(2d);
            entries.CriticalChance = 0f;
            entries.MultiPayChance = 1f;
            float[] samples = { 0.9f, 0.9f, 0.9f, 0.9f };
            int sampleIndex = 0;
            BankHarness harness = CreateHarness(entries, () => samples[sampleIndex++]);
            harness.UnlockBank();
            Value before = harness.Wallet.CurrentBalance;

            harness.Bank.Tick(25f);

            // Whole multi-pay chance of 1.0 grants a second payment without a fractional roll;
            // one critical sample is still consumed per payment.
            Assert.That(sampleIndex, Is.EqualTo(4));
            Assert.That(harness.Wallet.CurrentBalance, Is.EqualTo(before + new Value(8d)));
        }

        [Test]
        public void Tick_AggregatesBalanceAndPublishesOneCredit() {
            BankEntries entries = DefaultEntries();
            entries.PayoutAmount = new Value(500d);
            entries.MultiPayChance = 1f;
            BankHarness harness = CreateHarness(entries);
            harness.Wallet.Deserialize(new JObject { ["stored"] = 1d, ["degree"] = 4 });
            harness.UnlockBank();

            var credited = new List<Value>();
            using IDisposable creditedSubscription = harness.Wallet.Credited.Subscribe(credited.Add);
            Value before = harness.Wallet.CurrentBalance;

            harness.Bank.Tick(10f);

            Assert.That(credited, Has.Count.EqualTo(1));
            Assert.That(credited[0].Base.Degree, Is.EqualTo(1));
            Assert.That(credited[0].Stored, Is.EqualTo(1d).Within(0.0000001d));
            Assert.That(harness.Wallet.CurrentBalance, Is.EqualTo(before + new Value(1000d)));
        }

        [Test]
        public void Tick_CapsPayoutsAndRetainsBacklog() {
            BankEntries entries = DefaultEntries();
            entries.PayoutIntervalSeconds = 1f;
            BankHarness harness = CreateHarness(entries);
            harness.UnlockBank();
            Value before = harness.Wallet.CurrentBalance;

            harness.Bank.Tick(300f);
            Assert.That(harness.Wallet.CurrentBalance,
                Is.EqualTo(before + new Value(BankService.MaxPayoutsPerTick)));
            Assert.That(harness.Bank.ElapsedSeconds, Is.EqualTo(44d).Within(0.0001d));

            harness.Bank.Tick(0.25f);
            Assert.That(harness.Wallet.CurrentBalance, Is.EqualTo(before + new Value(300d)));
            Assert.That(harness.Bank.ElapsedSeconds, Is.EqualTo(0.25d).Within(0.0001d));
        }

        [Test]
        public void Tick_InterpretsAbsoluteElapsedSecondsUsingCurrentInterval() {
            BankEntries entries = DefaultEntries();
            var calculator = new MutableCalculator<BankEntries>(entries);
            BankHarness harness = CreateHarness(entries, bankCalculator: calculator);
            harness.UnlockBank();
            Value before = harness.Wallet.CurrentBalance;

            harness.Bank.Tick(8f);
            entries.PayoutIntervalSeconds = 20f;
            calculator.Value = entries;
            harness.InvalidateBankData();
            harness.Bank.Tick(1f);
            Assert.That(harness.Wallet.CurrentBalance, Is.EqualTo(before));
            Assert.That(harness.Bank.ElapsedSeconds, Is.EqualTo(9d).Within(0.0001d));

            entries.PayoutIntervalSeconds = 5f;
            calculator.Value = entries;
            harness.InvalidateBankData();
            harness.Bank.Tick(1f);
            Assert.That(harness.Wallet.CurrentBalance, Is.EqualTo(before + new Value(2d)));
            Assert.That(harness.Bank.ElapsedSeconds, Is.Zero);
        }

        [Test]
        public void Tick_IgnoresInvalidDeltasAndSaturatesExtremePayoutAggregation() {
            BankEntries entries = DefaultEntries();
            entries.PayoutAmount = BankCacheCalculator.MaximumPayoutAmount;
            entries.PayoutIntervalSeconds = 1f;
            entries.CriticalChance = 1f;
            entries.CriticalMultiplier = double.MaxValue;
            BankHarness harness = CreateHarness(entries, () => 0f);
            harness.UnlockBank();
            harness.Wallet.Deserialize(new JObject { ["stored"] = 0d, ["degree"] = 0 });

            Assert.DoesNotThrow(() => {
                harness.Bank.Tick(float.NaN);
                harness.Bank.Tick(float.PositiveInfinity);
                harness.Bank.Tick(-1f);
                harness.Bank.Tick(BankService.MaxPayoutsPerTick);
            });

            Assert.That(harness.Wallet.CurrentBalance,
                Is.GreaterThan(BankCacheCalculator.MaximumPayoutAmount));
        }

        [Test]
        public void Compensation_UsesBankRatioAndReturnsExactLargeDebitAtOneHundredPercent() {
            BankEntries entries = DefaultEntries();
            entries.BillCostCompensationRatio = 1d;
            BankHarness harness = CreateHarness(entries);
            harness.UnlockBank();
            var large = new Value(999d, new BaseValue(500));
            harness.Wallet.Deserialize(new JObject { ["stored"] = large.Stored, ["degree"] = large.Base.Degree });
            Assert.That(harness.Wallet.TryWithdrawWallet(large, false), Is.True);

            Value credited = Value.Zero;
            Assert.DoesNotThrow(() => credited = harness.Bank.ApplyBillCostCompensation(large));

            Assert.That(credited, Is.EqualTo(large));
            Assert.That(harness.Wallet.CurrentBalance, Is.EqualTo(large));
        }

        [Test]
        public void Save_RoundTripsAndMalformedRestoreIsAtomicForActiveAndDeferredState() {
            BankHarness source = CreateHarness(DefaultEntries());
            source.UnlockBank();
            source.Bank.Tick(3f);
            JToken saved = source.Bank.Serialize();

            Assert.Throws<JsonSerializationException>(() => source.Bank.Deserialize(
                new JObject { ["elapsedSeconds"] = double.NaN }));
            Assert.That(source.Bank.ElapsedSeconds, Is.EqualTo(3d).Within(0.0001d));

            var deferredBank = new BankService(null, Observable.Empty<float>());
            deferredBank.Deserialize(saved);
            Assert.Throws<JsonSerializationException>(() => deferredBank.Deserialize(
                new JObject { ["elapsedSeconds"] = -1d }));
            BankHarness restored = CreateHarness(DefaultEntries(), bank: deferredBank);
            Assert.That(restored.Bank.ElapsedSeconds, Is.EqualTo(3d).Within(0.0001d));
        }

        [Test]
        public void UpdateSubscription_IsDisposedWithService() {
            var updates = new Subject<float>();
            _disposables.Add(updates);
            BankHarness harness = CreateHarness(DefaultEntries(), updates: updates);
            harness.UnlockBank();
            harness.Bank.PostInitializeAsync(harness.Scope).GetAwaiter().GetResult();
            updates.OnNext(10f);
            Value afterFirstPayout = harness.Wallet.CurrentBalance;

            harness.Dispose();
            updates.OnNext(10f);

            Assert.That(harness.Wallet.CurrentBalance, Is.EqualTo(afterFirstPayout));
        }

        [Test]
        public void CacheValidation_RejectsInvalidAuthoredValuesAndNormalizesEffectiveValues() {
            BankEntries invalid = DefaultEntries();
            invalid.PayoutIntervalSeconds = 0f;
            Assert.Throws<InvalidOperationException>(() => BankCacheCalculator.ValidateBase(invalid));

            invalid = DefaultEntries();
            invalid.CriticalChance = float.NaN;
            Assert.Throws<InvalidOperationException>(() => BankCacheCalculator.ValidateBase(invalid));

            BankEntries effective = DefaultEntries();
            effective.PayoutIntervalSeconds = float.NaN;
            effective.CriticalChance = 2f;
            effective.CriticalMultiplier = 0d;
            effective.BillCostCompensationRatio = -1d;
            LogAssert.Expect(LogType.Warning, "Invalid effective bank values were normalized to safe ranges.");

            BankEntries normalized = BankCacheCalculator.NormalizeEffective(effective);

            Assert.That(normalized.PayoutIntervalSeconds,
                Is.EqualTo(BankCacheCalculator.DefaultPayoutIntervalSeconds));
            Assert.That(normalized.CriticalChance, Is.EqualTo(1f));
            Assert.That(normalized.CriticalMultiplier, Is.EqualTo(1d));
            Assert.That(normalized.BillCostCompensationRatio, Is.Zero);
        }

        private BankHarness CreateHarness(
            BankEntries entries,
            Func<float> random = null,
            Observable<float> updates = null,
            MutableCalculator<BankEntries> bankCalculator = null,
            BankService bank = null) {
            UpgradeNodeDefinition bankUnlock = CreateUpgrade("bank_unlock", FeatureIds.Bank);
            var provider = new FakeAssetProvider(new[] { bankUnlock });
            var scope = new ServiceScope(null);
            var wallet = new WalletService();
            wallet.ReplenishWallet(new Value(100d));
            var cache = new CacheVersionService();
            var upgrades = new UpgradeService(provider);
            var unlocks = new UnlockService();
            bankCalculator ??= new MutableCalculator<BankEntries>(entries);
            var stash = new PlayerStatStash();
            bank ??= new BankService(random, updates ?? Observable.Empty<float>());

            scope.Register(wallet)
                .Register(cache, typeof(ICacheInvalidator), typeof(ICacheVersionProvider))
                .Register(upgrades)
                .Register(unlocks)
                .Register<ICacheCalculator<IncomeEntries>>(new StaticCalculator<IncomeEntries>(default))
                .Register<ICacheCalculator<SignatureEntries>>(new StaticCalculator<SignatureEntries>(default))
                .Register<ICacheCalculator<GenerationEntries>>(new StaticCalculator<GenerationEntries>(default))
                .Register<ICacheCalculator<OfficeEntries>>(new StaticCalculator<OfficeEntries>(default))
                .Register<ICacheCalculator<BankEntries>>(bankCalculator)
                .Register<ICacheCalculator<DocumentEntries>>(new StaticCalculator<DocumentEntries>(default))
                .Register(stash)
                .Register(bank);

            upgrades.InitializeAsync(scope).GetAwaiter().GetResult();
            SetAllAvailable(upgrades);
            unlocks.InitializeAsync(scope).GetAwaiter().GetResult();
            stash.PreInitializeAsync(scope).GetAwaiter().GetResult();
            bank.InitializeAsync(scope).GetAwaiter().GetResult();

            var harness = new BankHarness(scope, wallet, cache, upgrades, bank);
            _disposables.Add(harness);
            return harness;
        }

        private UpgradeNodeDefinition CreateUpgrade(string id, params string[] features) {
            var definition = ScriptableObject.CreateInstance<UpgradeNodeDefinition>();
            definition.Id = id;
            definition.Name = id;
            definition.MaxLevel = 1;
            definition.CostFormula = new ConstantValue { Value = Value.One };
            definition.Modifiers = Array.Empty<ModifierDefinition>();
            definition.FeatureUnlockIds = features;
            definition.StatisticRequirements = Array.Empty<GameStatisticRequirement>();
            definition.Children = Array.Empty<UpgradeNodeLink>();
            _objects.Add(definition);
            return definition;
        }

        private static BankEntries DefaultEntries() {
            return new BankEntries {
                PayoutAmount = Value.One,
                PayoutIntervalSeconds = 10f,
                CriticalChance = 0f,
                CriticalMultiplier = 2d,
                BillCostCompensationRatio = 0d
            };
        }

        private static void SetAllAvailable(UpgradeService upgrades) {
            var availability = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (UpgradeNodeState state in upgrades.Nodes) availability.Add(state.Definition.Id, true);
            upgrades.ApplyAvailabilityBatch(availability);
        }

        private static void CompleteUpgrade(UpgradeService upgrades, string id) {
            Assert.That(upgrades.TryUpgrade(id), Is.True);
            Assert.That(upgrades.TryClaimPendingUpgrade(out UpgradeService.UpgradeDocumentClaim claim), Is.True);
            Assert.That(upgrades.TryCompletePendingUpgrade(claim, new Data.Results.SignatureEvaluationResult(
                SignatureEvaluationStatus.Accepted,
                SignatureFailureReason.None,
                1f,
                0.4f,
                null)), Is.True);
        }

        private sealed class BankHarness : IDisposable {
            private bool _disposed;

            public ServiceScope Scope { get; }
            public WalletService Wallet { get; }
            public CacheVersionService Cache { get; }
            public UpgradeService Upgrades { get; }
            public BankService Bank { get; }

            public BankHarness(ServiceScope scope, WalletService wallet, CacheVersionService cache,
                UpgradeService upgrades, BankService bank) {
                Scope = scope;
                Wallet = wallet;
                Cache = cache;
                Upgrades = upgrades;
                Bank = bank;
            }

            public void UnlockBank() => CompleteUpgrade(Upgrades, "bank_unlock");

            public void InvalidateBankData() => ((ICacheInvalidator)Cache).Invalidate<BankEntries>();

            public void Dispose() {
                if (_disposed) return;
                _disposed = true;
                Scope.Dispose();
            }
        }

        private sealed class MutableCalculator<T> : ICacheCalculator<T>, IService where T : struct {
            public T Value { get; set; }
            public MutableCalculator(T value) => Value = value;
            public T Calculate() => Value;
            public void Dispose() { }
        }

        private sealed class StaticCalculator<T> : ICacheCalculator<T>, IService where T : struct {
            private readonly T _value;
            public StaticCalculator(T value) => _value = value;
            public T Calculate() => _value;
            public void Dispose() { }
        }

        private sealed class FakeAssetProvider : IAssetProvider, IService {
            private readonly IReadOnlyList<UpgradeNodeDefinition> _upgrades;
            public FakeAssetProvider(IReadOnlyList<UpgradeNodeDefinition> upgrades) => _upgrades = upgrades;

            public UniTask<IAssetLease<T>> LoadAsync<T>(AssetReferenceT<T> reference) where T : UnityEngine.Object {
                throw new NotSupportedException();
            }

            public UniTask<IAssetListLease<T>> LoadAssetsByLabelAsync<T>(string label)
                where T : UnityEngine.Object {
                if (typeof(T) == typeof(UpgradeNodeDefinition)) {
                    var assets = new T[_upgrades.Count];
                    for (int index = 0; index < assets.Length; index++) assets[index] = (T)(object)_upgrades[index];
                    return UniTask.FromResult<IAssetListLease<T>>(new FakeAssetListLease<T>(assets));
                }
                throw new NotSupportedException(typeof(T).FullName);
            }

            public UniTask<IInstanceLease> InstantiateAsync(AssetReference instanceReference, Transform parent = null,
                bool worldPositionStays = false) {
                throw new NotSupportedException();
            }

            public void Dispose() { }
        }

        private sealed class FakeAssetListLease<T> : IAssetListLease<T> where T : UnityEngine.Object {
            public IReadOnlyList<T> Assets { get; }
            public FakeAssetListLease(IReadOnlyList<T> assets) => Assets = assets;
            public void Dispose() { }
        }
    }
}
