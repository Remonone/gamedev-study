using System;
using System.Collections.Generic;
using System.Reflection;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Documents;
using Data.Enums;
using Data.Formulas;
using Data.Modifiers.Calculation;
using Data.Results;
using Data.Rules;
using Data.Upgrades;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Presentation;
using R3;
using Services;
using Services.Locator;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Utils;
using Utils.Text.Generator;

namespace Tests.EditMode {
    public sealed class UpgradeDocumentWorkflowTests {
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
        public void FirstPurchase_WaitsForSignatureAndLaterLevelsStayImmediateAndPurchasable() {
            UpgradeNodeDefinition definition = CreateDefinition("upgrade", 3);
            UpgradeService upgrades = CreateUpgradeService(new[] { definition }, out WalletService wallet);
            SetAvailable(upgrades);

            Assert.That(upgrades.TryUpgrade("upgrade"), Is.True);
            UpgradeNodeState pending = upgrades.GetUpgrade("upgrade");
            Assert.That(pending.Level, Is.Zero);
            Assert.That(pending.CurrentState, Is.EqualTo(UpgradeNodeState.State.Pending));
            Assert.That(upgrades.OwnedUpgrades, Is.Empty);
            Assert.That(wallet.CurrentBalance, Is.EqualTo(new Value(99)));

            Assert.That(upgrades.TryClaimPendingUpgrade(out UpgradeService.UpgradeDocumentClaim claim), Is.True);
            Assert.That(upgrades.TryCompletePendingUpgrade(claim, Evaluation(
                SignatureEvaluationStatus.Rejected, 0.2f, 0.4f)), Is.True);
            Assert.That(upgrades.GetUpgrade("upgrade").Level, Is.EqualTo(1));
            Assert.That(upgrades.GetUpgrade("upgrade").Effectiveness, Is.EqualTo(0.5f).Within(0.0001f));

            Assert.That(upgrades.TryUpgrade("upgrade"), Is.True);
            Assert.That(upgrades.GetUpgrade("upgrade").Level, Is.EqualTo(2));
            Assert.That(upgrades.GetUpgrade("upgrade").Effectiveness, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(upgrades.CanUpgrade("upgrade"), Is.True,
                "A later level should remain purchasable after synchronous wallet/upgrade notifications.");
            Assert.That(upgrades.TryClaimPendingUpgrade(out _), Is.False);
        }

        [Test]
        public void PurchaseNotification_IsCommittedAndRejectsReentrantMutation() {
            UpgradeNodeDefinition definition = CreateDefinition("upgrade", 2);
            UpgradeService upgrades = CreateUpgradeService(new[] { definition }, out WalletService wallet);
            SetAvailable(upgrades);
            bool observe = false;
            bool reentrantResult = true;
            JObject snapshot = null;
            using IDisposable subscription = wallet.BalanceChanged.Subscribe(_ => {
                if (!observe) return;
                snapshot = (JObject)upgrades.Serialize();
                reentrantResult = upgrades.TryUpgrade("upgrade");
            });

            observe = true;
            Assert.That(upgrades.TryUpgrade("upgrade"), Is.True);

            Assert.That(reentrantResult, Is.False);
            Assert.That((snapshot?["pendingUpgrades"] as JArray)?.Count, Is.EqualTo(1));
            Assert.That(upgrades.GetUpgrade("upgrade").CurrentState, Is.EqualTo(UpgradeNodeState.State.Pending));
        }

        [Test]
        public void Restore_InvalidatesStaleClaimAndReissuesPendingDocument() {
            UpgradeNodeDefinition definition = CreateDefinition("upgrade");
            UpgradeService upgrades = CreateUpgradeService(new[] { definition }, out _);
            SetAvailable(upgrades);
            Assert.That(upgrades.TryUpgrade("upgrade"), Is.True);

            var producer = new UpgradeDocumentProducer();
            ServiceScope scope = GetScope(upgrades);
            producer.InitializeAsync(scope).GetAwaiter().GetResult();
            Assert.That(producer.TryProduce(out IDocumentSession staleSession), Is.True);
            JObject saved = (JObject)upgrades.Serialize();

            upgrades.Deserialize(saved);

            Assert.That(staleSession.TryProcess(Evaluation(SignatureEvaluationStatus.Accepted, 1f, 0.4f)), Is.False);
            staleSession.Dispose();
            Assert.That(producer.TryProduce(out IDocumentSession restoredSession), Is.True);
            Assert.That(restoredSession.TryProcess(Evaluation(SignatureEvaluationStatus.Accepted, 1f, 0.4f)), Is.True);
            restoredSession.Dispose();
            Assert.That(upgrades.GetUpgrade("upgrade").Level, Is.EqualTo(1));
        }

        [Test]
        public void UpgradePolicy_ExcludesOrdinaryPlayerSignatureModifiers() {
            UpgradeNodeDefinition definition = CreateDefinition("upgrade");
            UpgradeService upgrades = CreateUpgradeService(new[] { definition }, out _);
            SetAvailable(upgrades);
            Assert.That(upgrades.TryUpgrade("upgrade"), Is.True);
            var producer = new UpgradeDocumentProducer();
            producer.InitializeAsync(GetScope(upgrades)).GetAwaiter().GetResult();
            Assert.That(producer.TryProduce(out IDocumentSession session), Is.True);
            var baseRules = new SignatureDifficultyRules("base", 0.4f, 1f, 1f, 1f, null);
            var playerModifiers = new SignatureRuleModifiers(2f, -0.2f, 2f, 2f, 2f);

            DocumentEvaluationInputs inputs = session.EvaluationPolicy.Resolve(baseRules, playerModifiers);

            Assert.That(inputs.Difficulty, Is.SameAs(baseRules));
            Assert.That(inputs.Modifiers.MinimumSimilarityOffset, Is.Zero);
            Assert.That(inputs.Modifiers.CorridorWidthMultiplier, Is.EqualTo(1f));
            session.Dispose();
        }

        [Test]
        public void UpgradeNotification_CanReleaseAnotherClaimForImmediateReissue() {
            UpgradeNodeDefinition first = CreateDefinition("first");
            UpgradeNodeDefinition second = CreateDefinition("second");
            UpgradeService upgrades = CreateUpgradeService(new[] { first, second }, out _);
            SetAvailable(upgrades);
            Assert.That(upgrades.TryUpgrade("first"), Is.True);
            Assert.That(upgrades.TryUpgrade("second"), Is.True);
            var producer = new UpgradeDocumentProducer();
            producer.InitializeAsync(GetScope(upgrades)).GetAwaiter().GetResult();
            Assert.That(producer.TryProduce(out IDocumentSession firstSession), Is.True);
            Assert.That(producer.TryProduce(out IDocumentSession secondSession), Is.True);
            bool releaseDuringNotification = true;
            using IDisposable subscription = upgrades.Changed.Subscribe(_ => {
                if (releaseDuringNotification) secondSession.Dispose();
            });

            Assert.That(firstSession.TryProcess(Evaluation(
                SignatureEvaluationStatus.Accepted, 1f, 0.4f)), Is.True);
            releaseDuringNotification = false;

            Assert.That(producer.TryProduce(out IDocumentSession reissuedSecond), Is.True);
            reissuedSecond.Dispose();
            firstSession.Dispose();
        }

        [Test]
        public void UnknownPendingRestore_RefundsAfterCommitAndRejectsReentrantPurchase() {
            UpgradeNodeDefinition definition = CreateDefinition("known", 3);
            UpgradeService upgrades = CreateUpgradeService(new[] { definition }, out WalletService wallet);
            bool observe = false;
            bool reentrantResult = true;
            using IDisposable subscription = wallet.BalanceChanged.Subscribe(_ => {
                if (observe) reentrantResult = upgrades.TryUpgrade("known");
            });
            observe = true;
            LogAssert.Expect(LogType.Warning,
                "Pending upgrade 'removed' is not present in the loaded catalog; its paid cost was refunded.");

            upgrades.Deserialize(new JObject {
                ["upgrades"] = new JArray(new JObject {
                    ["id"] = "known", ["level"] = 1, ["effectiveness"] = 0.75f
                }),
                ["pendingUpgrades"] = new JArray(new JObject {
                    ["id"] = "removed", ["paidStored"] = 7d, ["paidDegree"] = 0
                })
            });

            Assert.That(reentrantResult, Is.False);
            Assert.That(wallet.CurrentBalance, Is.EqualTo(new Value(107)));
            Assert.That(upgrades.GetUpgrade("known").Level, Is.EqualTo(1));
            Assert.That(upgrades.CanUpgrade("known"), Is.True);
        }

        [Test]
        public void MalformedRestore_DoesNotReplacePendingState() {
            UpgradeNodeDefinition definition = CreateDefinition("known");
            UpgradeService upgrades = CreateUpgradeService(new[] { definition }, out WalletService wallet);
            SetAvailable(upgrades);
            Assert.That(upgrades.TryUpgrade("known"), Is.True);
            UpgradeNodeState before = upgrades.GetUpgrade("known");
            Value balance = wallet.CurrentBalance;

            Assert.Throws<JsonSerializationException>(() => upgrades.Deserialize(new JObject {
                ["upgrades"] = new JArray(new JObject { ["id"] = "known", ["level"] = 1 }),
                ["pendingUpgrades"] = new JArray(new JObject {
                    ["id"] = "known", ["paidStored"] = 1d, ["paidDegree"] = 0
                })
            }));

            Assert.That(upgrades.GetUpgrade("known"), Is.SameAs(before));
            Assert.That(wallet.CurrentBalance, Is.EqualTo(balance));
        }

        [Test]
        public void DocumentReservation_SaveReturnsIssuedDocumentAndInvalidatesStaleHandle() {
            var generator = Track(new DocumentGeneratorService());
            Assert.That(generator.TryReserveDocument(out DocumentGeneratorService.DocumentReservation stale), Is.True);
            JObject save = (JObject)generator.Serialize();
            Assert.That(save["documentQuantity"]?.Value<int>(), Is.EqualTo(1));

            generator.Deserialize(save);

            Assert.That(generator.TryCommitReservation(stale), Is.False);
            Assert.That(generator.TryCancelReservation(stale), Is.False);
            Assert.That(generator.TryReserveDocument(out DocumentGeneratorService.DocumentReservation current), Is.True);
            Assert.That(generator.TryCommitReservation(current), Is.True);
            Assert.That(generator.TryObtainDocument(), Is.False);
        }

        [Test]
        public void DispenseViewModel_UsesPriorityThenRegistrationOrderAndTransfersSessionOnce() {
            var lowSession = new FakeSession();
            var firstHighSession = new FakeSession();
            var secondHighSession = new FakeSession();
            var producers = new IDocumentProducer[] {
                new FakeProducer(0, lowSession),
                new FakeProducer(10, firstHighSession),
                new FakeProducer(10, secondHighSession)
            };
            var cache = new StaticCache<DocumentEntries>(new DocumentEntries { SelectedDocumentQualityLevel = 2 });
            var viewModel = new DispenseViewModel(producers, cache, new StableRandom(123));

            Assert.That(viewModel.TryCreateContext(out IDocumentContext context), Is.True);
            IDocumentSession selected = context.TakeSession();

            Assert.That(selected, Is.SameAs(firstHighSession));
            Assert.Throws<InvalidOperationException>(() => context.TakeSession());
            context.Dispose();
            selected.Dispose();
            Assert.That(firstHighSession.DisposeCount, Is.EqualTo(1));
            Assert.That(lowSession.DisposeCount, Is.Zero);
            Assert.That(secondHighSession.DisposeCount, Is.Zero);
        }

        [Test]
        public void NumericModifierEffectiveness_HandlesZeroPartialNaNAndInfinity() {
            Assert.That(NumericModifierCalculator.Apply(10d, NumericModifierOperation.Add, 5d, 0d), Is.EqualTo(10d));
            Assert.That(NumericModifierCalculator.Apply(10d, NumericModifierOperation.Add, 6d, 0.5d), Is.EqualTo(13d));
            Assert.That(NumericModifierCalculator.Apply(10d, NumericModifierOperation.Multiply, 2d, 0.5d), Is.EqualTo(15d));
            Assert.That(NumericModifierCalculator.Apply(10d, NumericModifierOperation.Override, 20d, 0.5d), Is.EqualTo(15d));
            Assert.That(NumericModifierCalculator.Apply(10d, NumericModifierOperation.Add, double.NaN, 1d), Is.EqualTo(10d));
            Assert.That(double.IsPositiveInfinity(NumericModifierCalculator.Apply(
                10d, NumericModifierOperation.Override, double.PositiveInfinity, 0.5d)), Is.True);
        }

        [Test]
        public void UpgradeDetailsView_RebindDoesNotDuplicateOwnedButtonListener() {
            var root = TrackObject(new GameObject("UpgradeDetailsTest"));
            var buttonObject = TrackObject(new GameObject("BuyButton"));
            var button = buttonObject.AddComponent<Button>();
            var view = root.AddComponent<UpgradeDetailsView>();
            SetField(view, "_panelRoot", root);
            SetField(view, "_buyButton", button);
            var model = new UpgradeNodePresentationModel(
                "upgrade", "Upgrade", "", null, Vector2.zero, 0, 1, "1", true, true,
                false, 0f, true);
            int purchaseCount = 0;

            view.Show(model, _ => { purchaseCount++; return true; });
            view.Show(model, _ => { purchaseCount++; return true; });
            button.onClick.Invoke();

            Assert.That(purchaseCount, Is.EqualTo(1));
        }

        private UpgradeService CreateUpgradeService(
            IReadOnlyList<UpgradeNodeDefinition> definitions,
            out WalletService wallet) {
            var upgrades = new UpgradeService(new FakeAssetProvider(definitions));
            var scope = new ServiceScope(null);
            wallet = new WalletService();
            wallet.ReplenishWallet(new Value(100));
            var cache = new CacheVersionService();
            scope.Register(wallet)
                .Register(cache, typeof(ICacheInvalidator), typeof(ICacheVersionProvider))
                .Register(upgrades);
            Track(scope);
            upgrades.InitializeAsync(scope).GetAwaiter().GetResult();
            return upgrades;
        }

        private static ServiceScope GetScope(UpgradeService upgrades) {
            var field = typeof(UpgradeService).GetField("_wallet", BindingFlags.Instance | BindingFlags.NonPublic);
            var wallet = (WalletService)field?.GetValue(upgrades);
            var scope = new ServiceScope(null);
            scope.Register(wallet).Register(upgrades);
            return scope;
        }

        private UpgradeNodeDefinition CreateDefinition(string id, int maxLevel = 1) {
            var definition = TrackObject(ScriptableObject.CreateInstance<UpgradeNodeDefinition>());
            definition.Id = id;
            definition.Name = id;
            definition.MaxLevel = maxLevel;
            definition.CostFormula = new ConstantValue { Value = Value.One };
            definition.Modifiers = Array.Empty<Data.Modifiers.ModifierDefinition>();
            definition.StatisticRequirements = Array.Empty<GameStatisticRequirement>();
            definition.Children = Array.Empty<UpgradeNodeLink>();
            return definition;
        }

        private static void SetAvailable(UpgradeService upgrades) {
            var values = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (UpgradeNodeState state in upgrades.Nodes) values.Add(state.Definition.Id, true);
            upgrades.ApplyAvailabilityBatch(values);
        }

        private static SignatureEvaluationResult Evaluation(
            SignatureEvaluationStatus status,
            float similarity,
            float minimum) {
            return new SignatureEvaluationResult(
                status,
                status == SignatureEvaluationStatus.Accepted
                    ? SignatureFailureReason.None
                    : SignatureFailureReason.BelowSimilarityThreshold,
                similarity,
                minimum,
                null);
        }

        private static void SetField(object target, string name, object value) {
            typeof(UpgradeDetailsView).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }

        private T Track<T>(T disposable) where T : IDisposable {
            _disposables.Add(disposable);
            return disposable;
        }

        private T TrackObject<T>(T value) where T : UnityEngine.Object {
            _objects.Add(value);
            return value;
        }

        private sealed class StaticCache<T> : IReadOnlyCacheData<T> {
            public T Value { get; }
            public StaticCache(T value) => Value = value;
        }

        private sealed class FakeProducer : IDocumentProducer {
            private IDocumentSession _session;
            public int Priority { get; }

            public FakeProducer(int priority, IDocumentSession session) {
                Priority = priority;
                _session = session;
            }

            public bool TryProduce(out IDocumentSession session) {
                session = _session;
                _session = null;
                return session != null;
            }
        }

        private sealed class FakeSession : IDocumentSession {
            public int DisposeCount { get; private set; }
            public IDocumentEvaluationPolicy EvaluationPolicy { get; } = new PassthroughPolicy();
            public bool TryProcess(SignatureEvaluationResult result) => true;
            public void Dispose() => DisposeCount++;
        }

        private sealed class PassthroughPolicy : IDocumentEvaluationPolicy {
            public DocumentEvaluationInputs Resolve(
                SignatureDifficultyRules baseDifficulty,
                SignatureRuleModifiers playerModifiers) {
                return new DocumentEvaluationInputs(baseDifficulty, playerModifiers);
            }
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
