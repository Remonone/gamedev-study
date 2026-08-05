using System;
using System.Collections.Generic;
using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Enums;
using Data.Formulas;
using Data.Modifiers;
using Data.Results;
using Data.Upgrades;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Presentation;
using R3;
using Services;
using Services.Locator;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Utils;

namespace Tests.EditMode {
    public sealed class OfficePresentationTests {
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
        public void Slots_UseOnlyClerkPurchaseAndVacantStatesWithPendingCapacityReservations() {
            Harness harness = CreateHarness(3, new Value(100));
            var viewModel = Track(new OfficeViewModel(harness.Office, harness.Wallet));
            AssertStates(viewModel, OfficeSlotState.Purchase, OfficeSlotState.Vacant, OfficeSlotState.Vacant);

            Assert.That(harness.Office.TryStartClerkHire(Value.One), Is.True);
            Assert.That(harness.Office.TryStartClerkHire(Value.One), Is.True);
            AssertStates(viewModel, OfficeSlotState.Purchase, OfficeSlotState.Vacant, OfficeSlotState.Vacant);

            Assert.That(harness.Office.TryStartClerkHire(Value.One), Is.True);
            AssertStates(viewModel, OfficeSlotState.Vacant, OfficeSlotState.Vacant, OfficeSlotState.Vacant);

            Assert.That(harness.Office.TryClaimPendingClerkHire(out OfficeService.ClerkHireDocumentClaim claim),
                Is.True);
            Assert.That(harness.Office.TryCompletePendingClerkHire(claim, Accepted()), Is.True);
            AssertStates(viewModel, OfficeSlotState.Clerk, OfficeSlotState.Vacant, OfficeSlotState.Vacant);
        }

        [Test]
        public void ProgressOnlyOfficeChangesDoNotEmitSlotOrSummaryRefreshes() {
            Harness harness = CreateHarness(1, new Value(100));
            HireClerk(harness);
            var viewModel = Track(new OfficeViewModel(harness.Office, harness.Wallet));
            int slotChanges = 0;
            int summaryChanges = 0;
            using IDisposable slotSubscription = viewModel.SlotsChanged.Subscribe(_ => slotChanges++);
            using IDisposable summarySubscription = viewModel.SummaryChanged.Subscribe(_ => summaryChanges++);

            harness.Office.Tick(0.25f);

            Assert.That(harness.Office.Clerks[0].Progress, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(slotChanges, Is.Zero);
            Assert.That(summaryChanges, Is.Zero);
        }

        [Test]
        public void ZeroBalanceKeepsDefaultBidAndGainingFundsMakesItImmediatelyUsable() {
            Harness harness = CreateHarness(1, Value.Zero);
            var viewModel = Track(new OfficeViewModel(harness.Office, harness.Wallet));
            Assert.That(viewModel.CommittedBid, Is.EqualTo(Value.One));
            Assert.That(viewModel.BeginBidEdit(), Is.True);
            Assert.That(viewModel.PreviewBid, Is.EqualTo(Value.Zero));
            Assert.That(viewModel.CanConfirmBid, Is.False);
            viewModel.CancelBidEdit();
            Assert.That(viewModel.CommittedBid, Is.EqualTo(Value.One));
            int bidChanges = 0;
            using IDisposable bidSubscription = viewModel.BidChanged.Subscribe(_ => bidChanges++);

            harness.Wallet.ReplenishWallet(new Value(10));

            Assert.That(viewModel.CurrentBalance, Is.EqualTo(new Value(10)));
            Assert.That(bidChanges, Is.EqualTo(1),
                "The popup balance display must refresh even before a new preview is opened.");
            Assert.That(viewModel.BeginBidEdit(), Is.True);
            Assert.That(viewModel.PreviewBid, Is.EqualTo(Value.One));
            Assert.That(viewModel.CanConfirmBid, Is.True);
            viewModel.CancelBidEdit();
            Assert.That(viewModel.Slots[0].CanHire, Is.True);
        }

        [Test]
        public void BidScaleHandlesSubOneBalanceChangesAndInfinityWithoutLogarithmOfZero() {
            Assert.That(OfficeViewModel.ResolveBid(0.5f, Value.Zero), Is.EqualTo(Value.Zero));
            Value subOne = new(0.25d);
            Assert.That(OfficeViewModel.ResolveBid(0f, subOne), Is.EqualTo(subOne));
            Assert.That(OfficeViewModel.ResolveNormalizedBid(Value.One, Value.Zero), Is.Zero);

            Value maximum = OfficeViewModel.ResolveMaximumSelectable(Value.Infinity);
            Assert.That(maximum.Base.Degree, Is.EqualTo(int.MaxValue - 1));
            Assert.That(maximum.Stored, Is.LessThan(1000d));
            Assert.That(OfficeViewModel.ResolveBid(1f, Value.Infinity), Is.EqualTo(maximum));

            Harness harness = CreateHarness(1, new Value(100));
            var viewModel = Track(new OfficeViewModel(harness.Office, harness.Wallet));
            Assert.That(viewModel.BeginBidEdit(), Is.True);
            viewModel.SetBidSliderValue(1f);
            Assert.That(viewModel.PreviewBid, Is.EqualTo(new Value(100)));
            harness.Wallet.TryWithdrawWallet(new Value(90));
            Assert.That(viewModel.PreviewBid, Is.EqualTo(new Value(10)));
            Assert.That(viewModel.ConfirmBidEdit(), Is.True);
            Assert.That(viewModel.CommittedBid, Is.EqualTo(new Value(10)));
        }

        private Harness CreateHarness(int capacity, Value balance) {
            UpgradeNodeDefinition definition = ScriptableObject.CreateInstance<UpgradeNodeDefinition>();
            definition.Id = "office_unlock";
            definition.Name = "Office";
            definition.MaxLevel = 1;
            definition.CostFormula = new ConstantValue { Value = Value.One };
            definition.Modifiers = Array.Empty<ModifierDefinition>();
            definition.FeatureUnlockIds = new[] { FeatureIds.Office };
            definition.StatisticRequirements = Array.Empty<GameStatisticRequirement>();
            definition.Children = Array.Empty<UpgradeNodeLink>();
            _objects.Add(definition);

            var scope = new ServiceScope(null);
            var wallet = new WalletService();
            if (!balance.IsZero) wallet.ReplenishWallet(balance);
            var cache = new CacheVersionService();
            var upgrades = new UpgradeService(new FakeAssetProvider(new[] { definition }));
            var unlocks = new UnlockService();
            var documents = new DocumentGeneratorService();
            var statistics = new GameStatisticsService();
            var officeEntries = new OfficeEntries {
                ClerkCapacity = capacity,
                DocumentsPerSecondPerClerk = 1f,
                QualityCeiling = 1f,
                AcceptanceThreshold = 0.5f,
                RewardMultiplier = 0.5f,
                BaseClerkMultiplierMedian = 2d,
                ClerkMultiplierRangeStep = 0d,
                MinimumClerkMultiplier = 1d,
                MaximumHireSignatureMultiplier = 2d,
                SalaryReviewCostRatio = 0.5d
            };
            var stash = new PlayerStatStash();
            var office = new OfficeService(() => 0.5f, null);
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
                .Register<ICacheCalculator<OfficeEntries>>(new StaticCalculator<OfficeEntries>(officeEntries))
                .Register<ICacheCalculator<DocumentEntries>>(new StaticCalculator<DocumentEntries>(default))
                .Register(stash)
                .Register<IMoneyAggregator>(new TestMoneyAggregator(wallet))
                .Register(office);

            upgrades.InitializeAsync(scope).GetAwaiter().GetResult();
            upgrades.Deserialize(new JObject {
                ["upgrades"] = new JArray(new JObject {
                    ["id"] = definition.Id,
                    ["level"] = 1,
                    ["effectiveness"] = 1f
                })
            });
            unlocks.InitializeAsync(scope).GetAwaiter().GetResult();
            stash.PreInitializeAsync(scope).GetAwaiter().GetResult();
            office.InitializeAsync(scope).GetAwaiter().GetResult();
            var harness = new Harness(scope, wallet, office);
            _disposables.Add(harness);
            return harness;
        }

        private T Track<T>(T disposable) where T : IDisposable {
            _disposables.Add(disposable);
            return disposable;
        }

        private static void HireClerk(Harness harness) {
            Assert.That(harness.Office.TryStartClerkHire(Value.One), Is.True);
            Assert.That(harness.Office.TryClaimPendingClerkHire(out OfficeService.ClerkHireDocumentClaim claim),
                Is.True);
            Assert.That(harness.Office.TryCompletePendingClerkHire(claim, Accepted()), Is.True);
        }

        private static SignatureEvaluationResult Accepted() {
            return new SignatureEvaluationResult(
                SignatureEvaluationStatus.Accepted,
                SignatureFailureReason.None,
                0.4f,
                0.4f,
                null);
        }

        private static void AssertStates(OfficeViewModel viewModel, params OfficeSlotState[] states) {
            Assert.That(viewModel.Slots.Count, Is.EqualTo(states.Length));
            for (int index = 0; index < states.Length; index++) {
                Assert.That(viewModel.Slots[index].State, Is.EqualTo(states[index]), $"slot {index}");
            }
        }

        private sealed class Harness : IDisposable {
            public WalletService Wallet { get; }
            public OfficeService Office { get; }
            private ServiceScope Scope { get; }

            public Harness(ServiceScope scope, WalletService wallet, OfficeService office) {
                Scope = scope;
                Wallet = wallet;
                Office = office;
            }

            public void Dispose() => Scope.Dispose();
        }

        private sealed class StaticCalculator<T> : ICacheCalculator<T>, IService {
            private T Value { get; }
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

            public UniTask<IInstanceLease> InstantiateAsync(
                AssetReference instanceReference,
                Transform parent = null,
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
