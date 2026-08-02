using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Formulas;
using Data.Modifiers;
using Data.Modifiers.Calculation;
using Data.Modifiers.Numeric;
using Data.Modifiers.Providers;
using Data.Office;
using Data.Persistence;
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
using Utils.Metadata;

namespace Tests.EditMode {
    public sealed class OfficeSystemTests {
        private readonly List<UnityEngine.Object> _objects = new();
        private readonly List<IDisposable> _disposables = new();

        [TearDown]
        public void TearDown() {
            for (int index = _disposables.Count - 1; index >= 0; index--) _disposables[index].Dispose();
            _disposables.Clear();
            for (int index = _objects.Count - 1; index >= 0; index--) {
                UnityEngine.Object.DestroyImmediate(_objects[index]);
            }
            _objects.Clear();
        }

        [Test]
        public void UnlockService_UnionsMultipleSourcesAndTracksRestoreRemoval() {
            UpgradeNodeDefinition first = CreateUpgrade("first", FeatureIds.Office);
            UpgradeNodeDefinition second = CreateUpgrade("second", FeatureIds.Office);
            using ServiceScope scope = CreateUpgradeScope(new[] { first, second }, out UpgradeService upgrades,
                out UnlockService unlocks);
            SetAllAvailable(upgrades);

            Assert.That(upgrades.TryUpgrade("first"), Is.True);
            Assert.That(unlocks.IsUnlocked(FeatureIds.Office), Is.True);
            Assert.That(upgrades.TryUpgrade("second"), Is.True);
            Assert.That(unlocks.UnlockedFeatures.Count, Is.EqualTo(1));

            upgrades.Deserialize(new JObject {
                ["upgrades"] = new JArray(new JObject { ["id"] = "second", ["level"] = 1 })
            });
            Assert.That(unlocks.IsUnlocked(FeatureIds.Office), Is.True);

            upgrades.Deserialize(new JObject { ["upgrades"] = new JArray() });
            Assert.That(unlocks.IsUnlocked(FeatureIds.Office), Is.False);
        }

        [Test]
        public void UnlockService_IgnoresMalformedAndDuplicateEntries() {
            UpgradeNodeDefinition definition = CreateUpgrade("invalid", "", FeatureIds.Office, FeatureIds.Office);
            LogAssert.Expect(LogType.Warning,
                "Upgrade 'invalid' contains an empty feature unlock at index 0; it was ignored.");
            LogAssert.Expect(LogType.Warning,
                "Upgrade 'invalid' grants feature 'office' more than once; the duplicate was ignored.");
            using ServiceScope scope = CreateUpgradeScope(new[] { definition }, out UpgradeService upgrades,
                out UnlockService unlocks);
            SetAllAvailable(upgrades);

            Assert.That(upgrades.TryUpgrade("invalid"), Is.True);
            Assert.That(unlocks.UnlockedFeatures, Is.EquivalentTo(new[] { FeatureIds.Office }));
        }

        [Test]
        public void NumericModifierPath_PreservesFractionsAndReturnsModifiedValue() {
            PredefinedMetadataWrapperStorage.Rebuild();
            IModifiableWrapper wrapper = PredefinedMetadataWrapperStorage.Get("Office");
            var source = CreateEntries(speed: 0.4f);

            var modified = (OfficeEntries)wrapper.Apply(source, nameof(OfficeEntries.DocumentsPerSecondPerClerk),
                NumericModifierOperation.Override, 0.75d);
            Assert.That(modified.DocumentsPerSecondPerClerk, Is.EqualTo(0.75f).Within(0.0001f));

            using var scope = new ServiceScope(null);
            var storage = new ModifierStorage();
            storage.RegisterProvider(new FractionalOfficeProvider());
            var service = new ModifierService();
            scope.Register(storage).Register<IModifierService>(service);
            service.InitializeAsync(scope).GetAwaiter().GetResult();

            OfficeEntries collected = service.Apply(source);
            Assert.That(collected.QualityCeiling, Is.EqualTo(0.625f).Within(0.0001f));
        }

        [Test]
        public void OfficeConfiguration_ValidatesBaseAndNormalizesUnsafeEffectiveValues() {
            OfficeEntries valid = CreateEntries();
            Assert.DoesNotThrow(() => OfficeCacheCalculator.ValidateBase(valid));
            valid.ClerkCapacity = 257;
            Assert.Throws<InvalidOperationException>(() => OfficeCacheCalculator.ValidateBase(valid));

            OfficeEntries unsafeValues = CreateEntries();
            unsafeValues.DocumentsPerSecondPerClerk = float.NaN;
            unsafeValues.QualityCeiling = float.PositiveInfinity;
            unsafeValues.AcceptanceThreshold = float.NaN;
            unsafeValues.RewardMultiplier = float.NegativeInfinity;
            LogAssert.Expect(LogType.Warning, "Invalid effective office values were normalized to safe ranges.");

            OfficeEntries safe = OfficeCacheCalculator.NormalizeEffective(unsafeValues);
            Assert.That(safe.DocumentsPerSecondPerClerk, Is.Zero);
            Assert.That(safe.QualityCeiling, Is.Zero);
            Assert.That(safe.AcceptanceThreshold, Is.EqualTo(1f));
            Assert.That(safe.RewardMultiplier, Is.Zero);
        }

        [Test]
        public void Hiring_RequiresUnlockFundsAndCapacityAndReactsToWalletChanges() {
            OfficeHarness harness = CreateHarness(CreateEntries(capacity: 2));
            Assert.That(harness.Office.CanHireClerk, Is.False);

            harness.UnlockOffice();
            Assert.That(harness.Office.CanHireClerk, Is.True);
            Assert.That(harness.Office.TryHireClerk(), Is.True);
            Assert.That(harness.Office.ClerkCount, Is.EqualTo(1));

            Assert.That(harness.Wallet.TryWithdrawWallet(new Value(98)), Is.True);
            Assert.That(harness.Office.CanHireClerk, Is.False);
            harness.Wallet.ReplenishWallet(new Value(10));
            Assert.That(harness.Office.CanHireClerk, Is.True);
            Assert.That(harness.Office.TryHireClerk(), Is.True);
            Assert.That(harness.Office.CanHireClerk, Is.False);
        }

        [Test]
        public void Tick_UsesRoundRobinOrderAndEmitsOneChangedNotification() {
            OfficeHarness harness = CreateHarness(CreateEntries(capacity: 2, speed: 2f), () => 1f);
            harness.UnlockOffice();
            Assert.That(harness.Office.TryHireClerk(), Is.True);
            Assert.That(harness.Office.TryHireClerk(), Is.True);
            SetDocumentCount(harness.Documents, 4);
            var order = new List<int>();
            int changes = 0;
            using IDisposable resultSubscription = harness.Office.DocumentProcessed.Subscribe(result => order.Add(result.ClerkId));
            using IDisposable changedSubscription = harness.Office.Changed.Subscribe(_ => changes++);

            harness.Office.Tick(1f);

            Assert.That(order, Is.EqualTo(new[] { 1, 2, 1, 2 }));
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(harness.Statistics.TryGetValue(GameStatisticIds.OfficeProcessedDocuments,
                out double processed), Is.True);
            Assert.That(processed, Is.EqualTo(4d));
        }

        [Test]
        public void Tick_RotatesScarceDocumentsAcrossClerksBetweenTicks() {
            OfficeHarness harness = CreateHarness(CreateEntries(capacity: 2), () => 1f);
            harness.UnlockOffice();
            Assert.That(harness.Office.TryHireClerk(), Is.True);
            Assert.That(harness.Office.TryHireClerk(), Is.True);
            var order = new List<int>();
            using IDisposable subscription = harness.Office.DocumentProcessed.Subscribe(result => order.Add(result.ClerkId));

            SetDocumentCount(harness.Documents, 1);
            harness.Office.Tick(1f);
            SetDocumentCount(harness.Documents, 1);
            harness.Office.Tick(1f);

            Assert.That(order, Is.EqualTo(new[] { 1, 2 }));
        }

        [Test]
        public void Tick_BlocksNestedTickAndRestoreUntilFinalNotificationCompletes() {
            OfficeHarness harness = CreateHarness(CreateEntries(), () => 1f);
            harness.UnlockOffice();
            Assert.That(harness.Office.TryHireClerk(), Is.True);
            SetDocumentCount(harness.Documents, 2);
            JToken restore = harness.Office.Serialize();
            int results = 0;
            int nestedTickAttempts = 0;
            using IDisposable resultSubscription = harness.Office.DocumentProcessed.Subscribe(_ => {
                results++;
                Assert.Throws<InvalidOperationException>(() => harness.Office.Deserialize(restore));
            });
            using IDisposable changedSubscription = harness.Office.Changed.Subscribe(_ => {
                nestedTickAttempts++;
                harness.Office.Tick(1f);
            });

            harness.Office.Tick(1f);

            Assert.That(results, Is.EqualTo(1));
            Assert.That(nestedTickAttempts, Is.EqualTo(1));
            Assert.That(harness.Office.Clerks[0].Progress, Is.Zero);
        }

        [Test]
        public void Tick_AcceptsAndRejectsUsingOfficeRewardRules() {
            var rolls = new Queue<float>(new[] { 1f, 0f });
            OfficeHarness harness = CreateHarness(CreateEntries(speed: 2f), () => rolls.Dequeue());
            harness.UnlockOffice();
            Assert.That(harness.Office.TryHireClerk(), Is.True);
            SetDocumentCount(harness.Documents, 2);
            var results = new List<OfficeDocumentResult>();
            using IDisposable subscription = harness.Office.DocumentProcessed.Subscribe(results.Add);
            double before = harness.Wallet.CurrentBalance.ToDouble();

            harness.Office.Tick(1f);

            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results[0].Accepted, Is.True);
            Assert.That(results[0].RequestedReward.ToDouble(), Is.EqualTo(0.5d).Within(0.0001d));
            Assert.That(results[0].CreditedReward.ToDouble(), Is.EqualTo(0.5d).Within(0.0001d));
            Assert.That(results[1].Accepted, Is.False);
            Assert.That(harness.Wallet.CurrentBalance.ToDouble(), Is.EqualTo(before + 0.5d).Within(0.0001d));
            AssertStatistic(harness.Statistics, GameStatisticIds.OfficeAcceptedDocuments, 1d);
            AssertStatistic(harness.Statistics, GameStatisticIds.OfficeRejectedDocuments, 1d);
        }

        [Test]
        public void Tick_StarvationStoresReadyStateAndInvalidDeltaDoesNotMutate() {
            OfficeHarness harness = CreateHarness(CreateEntries(speed: 1f));
            harness.UnlockOffice();
            Assert.That(harness.Office.TryHireClerk(), Is.True);
            SetDocumentCount(harness.Documents, 0);

            harness.Office.Tick(float.NaN);
            harness.Office.Tick(-1f);
            Assert.That(harness.Office.Clerks[0].Progress, Is.Zero);

            harness.Office.Tick(float.MaxValue);
            Assert.That(harness.Office.Clerks[0].Progress, Is.EqualTo(1f));
            SetDocumentCount(harness.Documents, 1);
            harness.Office.Tick(0.25f);
            Assert.That(harness.Office.Clerks[0].Progress, Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        public void CapacityDecreasePreservesClerksAndCacheInvalidationNotifies() {
            OfficeHarness harness = CreateHarness(CreateEntries(capacity: 2));
            harness.UnlockOffice();
            Assert.That(harness.Office.TryHireClerk(), Is.True);
            Assert.That(harness.Office.TryHireClerk(), Is.True);
            int notifications = 0;
            using IDisposable subscription = harness.Office.Changed.Subscribe(_ => notifications++);

            OfficeEntries reduced = harness.OfficeCalculator.Value;
            reduced.ClerkCapacity = 1;
            harness.OfficeCalculator.Value = reduced;
            ((ICacheInvalidator)harness.Cache).Invalidate<OfficeEntries>();

            Assert.That(harness.Office.ClerkCount, Is.EqualTo(2));
            Assert.That(harness.Office.ClerkCapacity, Is.EqualTo(1));
            Assert.That(harness.Office.CanHireClerk, Is.False);
            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void Persistence_RoundTripsAndMalformedRestoreIsAtomic() {
            OfficeHarness harness = CreateHarness(CreateEntries());
            harness.UnlockOffice();
            Assert.That(harness.Office.TryHireClerk(), Is.True);
            SetDocumentCount(harness.Documents, 0);
            harness.Office.Tick(0.25f);
            JToken saved = harness.Office.Serialize();

            harness.Office.Deserialize(new JObject {
                ["nextClerkId"] = 1,
                ["clerks"] = new JArray()
            });
            Assert.That(harness.Office.ClerkCount, Is.Zero);
            harness.Office.Deserialize(saved);
            Assert.That(harness.Office.ClerkCount, Is.EqualTo(1));
            Assert.That(harness.Office.Clerks[0].Progress, Is.EqualTo(0.25f).Within(0.0001f));
            JToken before = harness.Office.Serialize();

            Assert.Throws<JsonSerializationException>(() => harness.Office.Deserialize(new JObject {
                ["nextClerkId"] = 2,
                ["clerks"] = new JArray(
                    new JObject { ["id"] = 1, ["progress"] = 0.2f },
                    new JObject { ["id"] = 1, ["progress"] = 0.3f })
            }));
            Assert.That(JToken.DeepEquals(harness.Office.Serialize(), before), Is.True);
            AssertStatistic(harness.Statistics, GameStatisticIds.OfficeClerkCount, 1d);
        }

        [Test]
        public void StatisticsRestoreWithoutOfficeSection_ReconcilesCanonicalClerkCount() {
            OfficeHarness harness = CreateHarness(CreateEntries());
            harness.UnlockOffice();
            Assert.That(harness.Office.TryHireClerk(), Is.True);
            string path = Path.Combine(Path.GetTempPath(), $"SigningGame_Office_{Guid.NewGuid():N}.json");
            var save = new SaveService(path);
            harness.Scope.Register(save);
            save.PreInitializeAsync(harness.Scope).GetAwaiter().GetResult();
            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion, new Dictionary<string, JToken> {
                [harness.Statistics.SaveId] = new JObject()
            });

            Assert.That(save.LoadSnapshot(snapshot), Is.True);
            AssertStatistic(harness.Statistics, GameStatisticIds.OfficeClerkCount, 1d);
            if (File.Exists(path)) File.Delete(path);
        }

        [Test]
        public void PostInitialize_InjectedUpdateStreamDrivesTickAndDisposalStopsIt() {
            var updates = new Subject<float>();
            _disposables.Add(updates);
            OfficeHarness harness = CreateHarness(CreateEntries(), () => 1f, updates);
            harness.UnlockOffice();
            Assert.That(harness.Office.TryHireClerk(), Is.True);
            SetDocumentCount(harness.Documents, 2);
            harness.Office.PostInitializeAsync(harness.Scope).GetAwaiter().GetResult();
            int results = 0;
            using IDisposable subscription = harness.Office.DocumentProcessed.Subscribe(_ => results++);

            updates.OnNext(1f);
            Assert.That(results, Is.EqualTo(1));
            harness.Dispose();
            updates.OnNext(1f);
            Assert.That(results, Is.EqualTo(1));
        }

        private OfficeHarness CreateHarness(OfficeEntries entries, Func<float> random = null,
            Observable<float> updates = null) {
            UpgradeNodeDefinition definition = CreateUpgrade("office_unlock", FeatureIds.Office);
            var provider = new FakeAssetProvider(new[] { definition });
            var scope = new ServiceScope(null);
            var wallet = new WalletService();
            wallet.ReplenishWallet(new Value(100));
            var cache = new CacheVersionService();
            var upgrades = new UpgradeService(provider);
            var unlocks = new UnlockService();
            var documents = new DocumentGeneratorService();
            var statistics = new GameStatisticsService();
            var officeCalculator = new StaticCalculator<OfficeEntries>(entries);
            var stash = new PlayerStatStash();
            var money = new TestMoneyAggregator(wallet);
            var office = new OfficeService(random, updates);

            scope.Register(wallet)
                .Register(cache, typeof(ICacheInvalidator), typeof(ICacheVersionProvider))
                .Register(upgrades)
                .Register(unlocks)
                .Register(documents)
                .Register(statistics)
                .Register<ICacheCalculator<IncomeEntries>>(new StaticCalculator<IncomeEntries>(
                    new IncomeEntries(1f, 0.4f, Value.One)))
                .Register<ICacheCalculator<SignatureEntries>>(new StaticCalculator<SignatureEntries>(default))
                .Register<ICacheCalculator<GenerationEntries>>(new StaticCalculator<GenerationEntries>(default))
                .Register<ICacheCalculator<OfficeEntries>>(officeCalculator)
                .Register(stash)
                .Register<IMoneyAggregator>(money)
                .Register(office);

            upgrades.InitializeAsync(scope).GetAwaiter().GetResult();
            SetAllAvailable(upgrades);
            unlocks.InitializeAsync(scope).GetAwaiter().GetResult();
            stash.PreInitializeAsync(scope).GetAwaiter().GetResult();
            office.InitializeAsync(scope).GetAwaiter().GetResult();

            var harness = new OfficeHarness(scope, wallet, cache, upgrades, documents, statistics,
                officeCalculator, office);
            _disposables.Add(harness);
            return harness;
        }

        private ServiceScope CreateUpgradeScope(IReadOnlyList<UpgradeNodeDefinition> definitions,
            out UpgradeService upgrades, out UnlockService unlocks) {
            var scope = new ServiceScope(null);
            var wallet = new WalletService();
            wallet.ReplenishWallet(new Value(100));
            var cache = new CacheVersionService();
            upgrades = new UpgradeService(new FakeAssetProvider(definitions));
            unlocks = new UnlockService();
            scope.Register(wallet)
                .Register(cache, typeof(ICacheInvalidator), typeof(ICacheVersionProvider))
                .Register(upgrades)
                .Register(unlocks);
            upgrades.InitializeAsync(scope).GetAwaiter().GetResult();
            unlocks.InitializeAsync(scope).GetAwaiter().GetResult();
            return scope;
        }

        private UpgradeNodeDefinition CreateUpgrade(string id, params string[] featureIds) {
            var definition = ScriptableObject.CreateInstance<UpgradeNodeDefinition>();
            definition.Id = id;
            definition.Name = id;
            definition.MaxLevel = 1;
            definition.CostFormula = new ConstantValue { Value = Value.One };
            definition.Modifiers = Array.Empty<ModifierDefinition>();
            definition.FeatureUnlockIds = featureIds;
            definition.StatisticRequirements = Array.Empty<GameStatisticRequirement>();
            definition.Children = Array.Empty<UpgradeNodeLink>();
            _objects.Add(definition);
            return definition;
        }

        private static OfficeEntries CreateEntries(int capacity = 1, float speed = 1f) {
            return new OfficeEntries {
                ClerkCapacity = capacity,
                DocumentsPerSecondPerClerk = speed,
                QualityCeiling = 1f,
                AcceptanceThreshold = 0.5f,
                RewardMultiplier = 0.5f,
                BaseHireCost = Value.One,
                HireCostGrowthMultiplier = 1f
            };
        }

        private static void SetAllAvailable(UpgradeService upgrades) {
            var availability = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (UpgradeNodeState state in upgrades.Nodes) availability.Add(state.Definition.Id, true);
            upgrades.ApplyAvailabilityBatch(availability);
        }

        private static void SetDocumentCount(DocumentGeneratorService documents, int count) {
            documents.Deserialize(new JObject {
                ["documentQuantity"] = count,
                ["currentPoints"] = 0f
            });
        }

        private static void AssertStatistic(GameStatisticsService statistics, string id, double expected) {
            Assert.That(statistics.TryGetValue(id, out double value), Is.True, id);
            Assert.That(value, Is.EqualTo(expected), id);
        }

        private sealed class OfficeHarness : IDisposable {
            public ServiceScope Scope { get; }
            public WalletService Wallet { get; }
            public CacheVersionService Cache { get; }
            public UpgradeService Upgrades { get; }
            public DocumentGeneratorService Documents { get; }
            public GameStatisticsService Statistics { get; }
            public StaticCalculator<OfficeEntries> OfficeCalculator { get; }
            public OfficeService Office { get; }
            private bool _disposed;

            public OfficeHarness(ServiceScope scope, WalletService wallet, CacheVersionService cache,
                UpgradeService upgrades, DocumentGeneratorService documents, GameStatisticsService statistics,
                StaticCalculator<OfficeEntries> officeCalculator, OfficeService office) {
                Scope = scope;
                Wallet = wallet;
                Cache = cache;
                Upgrades = upgrades;
                Documents = documents;
                Statistics = statistics;
                OfficeCalculator = officeCalculator;
                Office = office;
            }

            public void UnlockOffice() {
                Assert.That(Upgrades.TryUpgrade("office_unlock"), Is.True);
            }

            public void Dispose() {
                if (_disposed) return;
                _disposed = true;
                Scope.Dispose();
            }
        }

        private sealed class StaticCalculator<T> : ICacheCalculator<T>, IService {
            public T Value { get; set; }
            public StaticCalculator(T value) => Value = value;
            public T Calculate() => Value;
            public void Dispose() { }
        }

        private sealed class TestMoneyAggregator : IMoneyAggregator, IService {
            private readonly WalletService _wallet;
            public TestMoneyAggregator(WalletService wallet) => _wallet = wallet;
            public Value AddMoney(Value amount) => _wallet.ReplenishWallet(amount) ? amount : Value.Zero;
            public void Dispose() { }
        }

        private sealed class FractionalOfficeProvider : IModifierProvider {
            public T Collect<T>(T target) where T : struct {
                if (target is not OfficeEntries office) return target;
                office.QualityCeiling = 0.625f;
                return (T)(object)office;
            }
            public void Init(IServiceScope scope) { }
        }

        private sealed class FakeAssetProvider : IAssetProvider {
            private readonly IReadOnlyList<UpgradeNodeDefinition> _definitions;
            public FakeAssetProvider(IReadOnlyList<UpgradeNodeDefinition> definitions) => _definitions = definitions;

            public UniTask<IAssetLease<T>> LoadAsync<T>(AssetReferenceT<T> reference) where T : UnityEngine.Object {
                throw new NotSupportedException();
            }

            public UniTask<IAssetListLease<T>> LoadAssetsByLabelAsync<T>(string label)
                where T : UnityEngine.Object {
                if (typeof(T) != typeof(UpgradeNodeDefinition)) throw new NotSupportedException();
                var values = new List<T>(_definitions.Count);
                for (int index = 0; index < _definitions.Count; index++) {
                    values.Add((T)(object)_definitions[index]);
                }
                return UniTask.FromResult<IAssetListLease<T>>(new FakeAssetListLease<T>(values));
            }

            public UniTask<IInstanceLease> InstantiateAsync(AssetReference instanceReference, Transform parent = null,
                bool worldPositionStays = false) {
                throw new NotSupportedException();
            }
        }

        private sealed class FakeAssetListLease<T> : IAssetListLease<T> where T : UnityEngine.Object {
            public IReadOnlyList<T> Assets { get; }
            public FakeAssetListLease(IReadOnlyList<T> assets) => Assets = assets;
            public void Dispose() { }
        }
    }
}
