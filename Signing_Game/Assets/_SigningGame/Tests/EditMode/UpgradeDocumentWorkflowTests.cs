using System;
using System.Collections.Generic;
using System.Reflection;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Documents;
using Data.Enums;
using Data.Formulas;
using Data.Modifiers;
using Data.Modifiers.Calculation;
using Data.Modifiers.Numeric;
using Data.Modifiers.Providers;
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
using UI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Utils;
using Utils.Metadata;
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
            var effectiveRules = baseRules with { MinimumSimilarity = 0.2f, CorridorWidthMultiplier = 2f };

            DocumentEvaluationInputs inputs = session.EvaluationPolicy.Resolve(
                new SignatureDifficultyContext(baseRules, effectiveRules));

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

            Assert.That(viewModel.TryCreateContext(viewModel.Current, out IDocumentContext context), Is.True);
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
        public void DispenseViewModel_AdvancesNormalPresentationAfterEachSuccessfulClaim() {
            var producer = new QueueProducer(new FakeSession(), new FakeSession());
            var cache = new StaticCache<DocumentEntries>(default);
            using var viewModel = new DispenseViewModel(
                new IDocumentProducer[] { producer }, cache, new StableRandom(123));
            DispensedDocumentPresentation first = viewModel.Current;

            Assert.That(viewModel.TryCreateContext(first, out IDocumentContext firstContext), Is.True);
            IDocumentSession firstSession = firstContext.TakeSession();
            firstContext.Dispose();
            viewModel.AdvanceAfterClaim(first.Key);
            DispensedDocumentPresentation second = viewModel.Current;

            Assert.That(second.Revision, Is.GreaterThan(first.Revision));
            Assert.That(second.TextSeed, Is.Not.EqualTo(first.TextSeed));
            firstSession.Dispose();
        }

        [Test]
        public void UpgradeOffer_ExposesDefinitionIdentityHeaderAndIconAndReissuesAfterRelease() {
            UpgradeNodeDefinition definition = CreateDefinition("upgrade");
            definition.Name = "Visible Upgrade";
            UpgradeService upgrades = CreateUpgradeService(new[] { definition }, out _);
            SetAvailable(upgrades);
            Assert.That(upgrades.TryUpgrade("upgrade"), Is.True);
            var producer = new UpgradeDocumentProducer();
            producer.InitializeAsync(GetScope(upgrades)).GetAwaiter().GetResult();

            Assert.That(producer.TryPeekOffer(out DocumentOffer offer), Is.True);
            Assert.That(offer.Key.Kind, Is.EqualTo(DocumentKind.Upgrade));
            Assert.That(offer.Key.DomainId, Is.EqualTo("upgrade"));
            Assert.That(offer.Header, Is.EqualTo("Visible Upgrade"));
            Assert.That(offer.Icon, Is.SameAs(definition.Icon));
            Assert.That(producer.TryProduce(offer.Key, out IDocumentSession session), Is.True);
            Assert.That(producer.TryPeekOffer(out _), Is.False);
            session.Dispose();
            Assert.That(producer.TryPeekOffer(out DocumentOffer reissued), Is.True);
            Assert.That(reissued.Key, Is.EqualTo(offer.Key));
        }

        [Test]
        public void DocumentDragView_ValidatesGeometryBeforeGateAndRejectedGateDoesNotStartDrag() {
            var eventSystemObject = TrackObject(new GameObject("EventSystem", typeof(EventSystem)));
            var eventSystem = eventSystemObject.GetComponent<EventSystem>();
            var orphan = TrackObject(new GameObject("Orphan", typeof(RectTransform), typeof(CanvasGroup),
                typeof(DocumentDragView)));
            var orphanDrag = orphan.GetComponent<DocumentDragView>();
            int orphanGateCalls = 0;
            orphanDrag.SetBeginDragGate(() => { orphanGateCalls++; return true; });
            var eventData = new PointerEventData(eventSystem) {
                button = PointerEventData.InputButton.Left,
                position = Vector2.zero,
                pointerId = 1
            };

            orphanDrag.OnBeginDrag(eventData);
            Assert.That(orphanGateCalls, Is.Zero);

            var parent = TrackObject(new GameObject("Parent", typeof(RectTransform)));
            orphan.transform.SetParent(parent.transform, false);
            int rejectedGateCalls = 0;
            int dragStarts = 0;
            orphanDrag.SetBeginDragGate(() => { rejectedGateCalls++; return false; });
            using IDisposable subscription = orphanDrag.IsDragging.Where(value => value).Subscribe(_ => dragStarts++);
            orphanDrag.OnBeginDrag(eventData);

            Assert.That(rejectedGateCalls, Is.EqualTo(1));
            Assert.That(dragStarts, Is.Zero);
        }

        [Test]
        public void UpgradeDescriptionFormatter_ReplacesKnownTokensAndHandlesSignedAndTerminalDeltas() {
            UpgradeNodeDefinition increasing = CreateDefinition("increasing", 0);
            increasing.Description = "Generation ${generation}; unknown ${missing}";
            increasing.Modifiers = new[] { CreateModifier("generation", new LevelValueDefinition()) };

            Assert.That(UpgradeDescriptionFormatter.Format(increasing, 0),
                Is.EqualTo("Generation 0(1); unknown ${missing}"));
            Assert.That(UpgradeDescriptionFormatter.Format(increasing, 2),
                Is.EqualTo("Generation 2(1); unknown ${missing}"));

            increasing.MaxLevel = 2;
            Assert.That(UpgradeDescriptionFormatter.Format(increasing, 2),
                Is.EqualTo("Generation 2(0); unknown ${missing}"));

            UpgradeNodeDefinition decreasing = CreateDefinition("decreasing", 0);
            decreasing.Description = "Penalty ${penalty}";
            decreasing.Modifiers = new[] { CreateModifier("penalty", new DescendingLevelValueDefinition()) };
            Assert.That(UpgradeDescriptionFormatter.Format(decreasing, 5), Is.EqualTo("Penalty 5(-1)"));
        }

        [Test]
        public void CatalogRejectsMissingAndDuplicateNumericModifierIds() {
            UpgradeNodeDefinition missing = CreateDefinition("missing");
            missing.Modifiers = new[] { CreateModifier(" ", new LevelValueDefinition()) };
            Assert.Throws<InvalidOperationException>(() => CreateUpgradeService(new[] { missing }, out _));

            UpgradeNodeDefinition duplicate = CreateDefinition("duplicate");
            duplicate.Modifiers = new[] {
                CreateModifier("same", new LevelValueDefinition()),
                CreateModifier("same", new LevelValueDefinition())
            };
            Assert.Throws<InvalidOperationException>(() => CreateUpgradeService(new[] { duplicate }, out _));
        }

        [Test]
        public void ZeroMaxLevel_IsUnlimitedUntilTechnicalIntegerLimit() {
            UpgradeNodeDefinition definition = CreateDefinition("unlimited", 0);
            UpgradeService upgrades = CreateUpgradeService(new[] { definition }, out _);
            SetAvailable(upgrades);
            Assert.That(upgrades.TryUpgrade("unlimited"), Is.True);
            Assert.That(upgrades.TryClaimPendingUpgrade(out UpgradeService.UpgradeDocumentClaim claim), Is.True);
            Assert.That(upgrades.TryCompletePendingUpgrade(claim,
                Evaluation(SignatureEvaluationStatus.Accepted, 1f, 0.4f)), Is.True);
            Assert.That(upgrades.GetUpgrade("unlimited").CurrentState,
                Is.EqualTo(UpgradeNodeState.State.InProgress));
            Assert.That(upgrades.TryUpgrade("unlimited"), Is.True);
            Assert.That(upgrades.GetUpgrade("unlimited").Level, Is.EqualTo(2));

            upgrades.Deserialize(new JObject {
                ["upgrades"] = new JArray(new JObject {
                    ["id"] = "unlimited", ["level"] = int.MaxValue - 1, ["effectiveness"] = 1f
                })
            });
            Assert.That(upgrades.GetUpgrade("unlimited").Level, Is.EqualTo(int.MaxValue - 1));
            Assert.That(upgrades.TryUpgrade("unlimited"), Is.True);
            Assert.That(upgrades.GetUpgrade("unlimited").Level, Is.EqualTo(int.MaxValue));
            Assert.That(upgrades.GetUpgrade("unlimited").CurrentState,
                Is.EqualTo(UpgradeNodeState.State.Completed));
            Assert.That(upgrades.TryUpgrade("unlimited"), Is.False);
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
        public void DeferredRestore_AppliesSavedEffectivenessWithCurrentModifierValues() {
            UpgradeNodeDefinition savedDefinition = CreateIncomeDefinition("income", 10d);
            UpgradeService source = CreateUpgradeService(new[] { savedDefinition }, out _);
            SetAvailable(source);
            Assert.That(source.TryUpgrade("income"), Is.True);
            Assert.That(source.TryClaimPendingUpgrade(out UpgradeService.UpgradeDocumentClaim claim), Is.True);
            Assert.That(source.TryCompletePendingUpgrade(claim,
                Evaluation(SignatureEvaluationStatus.Rejected, 0.2f, 0.4f)), Is.True);
            JObject save = (JObject)source.Serialize();

            UpgradeNodeDefinition restoredDefinition = CreateIncomeDefinition("income", 20d);
            var restored = new UpgradeService(new FakeAssetProvider(new[] { restoredDefinition }));
            var storage = new ModifierStorage();
            storage.RegisterProvider(new UpgradeModifierProvider());
            ServiceScope scope = CreateModifierScope(restored, storage);
            Track(scope);

            restored.Deserialize(save);
            restored.InitializeAsync(scope).GetAwaiter().GetResult();
            storage.PostInitializeAsync(scope).GetAwaiter().GetResult();

            IncomeEntries modified = storage.GetProvider<UpgradeModifierProvider>().Collect(
                new IncomeEntries(1f, 0.5f, new Value(10d)));
            Assert.That(restored.GetUpgrade("income").Effectiveness, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(modified.IncomePerDocument, Is.EqualTo(new Value(20d)));
        }

        [Test]
        public void RuntimeRestore_InvalidatesExistingCachesWhenModifierDefinitionChanged() {
            var modifierDefinition = TrackObject(ScriptableObject.CreateInstance<ModifierDefinition>());
            modifierDefinition.NumericModifiers = new List<NumericModifierDefinition>();
            modifierDefinition.GetAffectedTypes();
            UpgradeNodeDefinition definition = CreateDefinition("income");
            definition.Modifiers = new[] { modifierDefinition };

            var upgrades = new UpgradeService(new FakeAssetProvider(new[] { definition }));
            var storage = new ModifierStorage();
            storage.RegisterProvider(new UpgradeModifierProvider());
            var modifierService = new ModifierService();
            ServiceScope scope = CreateModifierScope(upgrades, storage, modifierService);
            Track(scope);
            modifierService.InitializeAsync(scope).GetAwaiter().GetResult();
            upgrades.InitializeAsync(scope).GetAwaiter().GetResult();
            storage.PostInitializeAsync(scope).GetAwaiter().GetResult();
            var cache = scope.Get<ICacheVersionProvider>();
            var cachedIncome = new CachedData<IncomeEntries>(
                cache,
                new ModifierBackedIncomeCalculator(
                    modifierService,
                    new IncomeEntries(1f, 0.5f, new Value(10d))));

            Assert.That(cachedIncome.Value.IncomePerDocument, Is.EqualTo(new Value(10d)));
            modifierDefinition.NumericModifiers.Add(CreateIncomeNumericModifier("income_per_document", 20d));

            upgrades.Deserialize(new JObject {
                ["upgrades"] = new JArray(new JObject {
                    ["id"] = "income", ["level"] = 1, ["effectiveness"] = 0.5f
                })
            });

            Assert.That(cachedIncome.Value.IncomePerDocument, Is.EqualTo(new Value(20d)));
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
                "upgrade", "Upgrade", "", null, Vector2.zero, 0, 1, "0/1", "1", true, true,
                false, 0f, true);
            int purchaseCount = 0;

            view.Show(model, _ => { purchaseCount++; return true; }, () => { });
            view.Show(model, _ => { purchaseCount++; return true; }, () => { });
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

        private ModifierDefinition CreateModifier(string id, NumericValueDefinition value) {
            var numeric = new NumericModifierDefinition();
            typeof(NumericModifierDefinition).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(numeric, id);
            typeof(NumericModifierDefinition).GetField("_value", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(numeric, value);
            var definition = TrackObject(ScriptableObject.CreateInstance<ModifierDefinition>());
            definition.NumericModifiers = new List<NumericModifierDefinition> { numeric };
            return definition;
        }

        private UpgradeNodeDefinition CreateIncomeDefinition(string id, double modifierBaseValue) {
            UpgradeNodeDefinition definition = CreateDefinition(id);
            definition.Modifiers = new[] { CreateIncomeModifier("income_per_document", modifierBaseValue) };
            return definition;
        }

        private ModifierDefinition CreateIncomeModifier(string id, double baseValue) {
            var definition = TrackObject(ScriptableObject.CreateInstance<ModifierDefinition>());
            definition.NumericModifiers = new List<NumericModifierDefinition> {
                CreateIncomeNumericModifier(id, baseValue)
            };
            return definition;
        }

        private NumericModifierDefinition CreateIncomeNumericModifier(string id, double baseValue) {
            var parameter = new CacheParameterReference();
            typeof(CacheParameterReference).GetField("_groupId", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(parameter, "Income");
            typeof(CacheParameterReference).GetField("_parameterId", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(parameter, nameof(IncomeEntries.IncomePerDocument));

            var value = new UpgradeNumericValueDefinition();
            typeof(UpgradeNumericValueDefinition).GetField("_baseValue", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(value, new Value(baseValue));
            typeof(UpgradeNumericValueDefinition).GetField("_formula", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(value, new ConstantValue { Value = Value.One });

            var numeric = new NumericModifierDefinition();
            typeof(NumericModifierDefinition).GetField("_id", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(numeric, id);
            typeof(NumericModifierDefinition).GetField("_operation", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(numeric, NumericModifierOperation.Add);
            typeof(NumericModifierDefinition).GetField("_value", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(numeric, value);
            typeof(NumericModifierDefinition).GetField("_parameter", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(numeric, parameter);

            return numeric;
        }

        private static ServiceScope CreateModifierScope(
            UpgradeService restored,
            ModifierStorage storage,
            ModifierService modifierService = null) {
            var scope = new ServiceScope(null);
            var wallet = new WalletService();
            wallet.ReplenishWallet(new Value(100));
            var cache = new CacheVersionService();
            scope.Register(wallet)
                .Register(cache, typeof(ICacheInvalidator), typeof(ICacheVersionProvider))
                .Register(storage);
            if (modifierService != null) scope.Register<IModifierService>(modifierService);
            scope.Register(restored);
            return scope;
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

        private sealed class ModifierBackedIncomeCalculator : ICacheCalculator<IncomeEntries> {
            private readonly IModifierService _modifierService;
            private readonly IncomeEntries _baseValue;

            public ModifierBackedIncomeCalculator(IModifierService modifierService, IncomeEntries baseValue) {
                _modifierService = modifierService;
                _baseValue = baseValue;
            }

            public IncomeEntries Calculate() {
                return _modifierService.Apply(_baseValue);
            }
        }

        private sealed class FakeProducer : IDocumentProducer {
            private IDocumentSession _session;
            private readonly Subject<Unit> _changed = new();
            public int Priority { get; }
            public Observable<Unit> OffersChanged => _changed;
            private DocumentOfferKey Key { get; }

            public FakeProducer(int priority, IDocumentSession session) {
                Priority = priority;
                _session = session;
                Key = new DocumentOfferKey(DocumentKind.Normal, Guid.NewGuid().ToString());
            }

            public bool TryPeekOffer(out DocumentOffer offer) {
                offer = _session == null ? null : new DocumentOffer(Key, true);
                return offer != null;
            }

            public bool TryProduce(DocumentOfferKey offerKey, out IDocumentSession session) {
                if (offerKey != Key) {
                    session = null;
                    return false;
                }

                session = _session;
                _session = null;
                return session != null;
            }
        }

        private sealed class QueueProducer : IDocumentProducer {
            private readonly Queue<IDocumentSession> _sessions;
            private readonly Subject<Unit> _changed = new();
            private readonly DocumentOfferKey _key = new(DocumentKind.Normal, "normal");
            public int Priority => 0;
            public Observable<Unit> OffersChanged => _changed;

            public QueueProducer(params IDocumentSession[] sessions) {
                _sessions = new Queue<IDocumentSession>(sessions);
            }

            public bool TryPeekOffer(out DocumentOffer offer) {
                offer = new DocumentOffer(_key, _sessions.Count > 0);
                return true;
            }

            public bool TryProduce(DocumentOfferKey offerKey, out IDocumentSession session) {
                if (offerKey != _key || _sessions.Count == 0) {
                    session = null;
                    return false;
                }

                session = _sessions.Dequeue();
                return true;
            }
        }

        private sealed class LevelValueDefinition : NumericValueDefinition {
            public override Value Evaluate(IModifierContext context) {
                return new Value(context.Require<LevelModifierCapability>().Level);
            }
        }

        private sealed class DescendingLevelValueDefinition : NumericValueDefinition {
            public override Value Evaluate(IModifierContext context) {
                return new Value(10 - context.Require<LevelModifierCapability>().Level);
            }
        }

        private sealed class FakeSession : IDocumentSession {
            public int DisposeCount { get; private set; }
            public DocumentKind Kind => DocumentKind.Normal;
            public IDocumentEvaluationPolicy EvaluationPolicy { get; } = new PassthroughPolicy();
            public bool TryProcess(SignatureEvaluationResult result) => true;
            public void Dispose() => DisposeCount++;
        }

        private sealed class PassthroughPolicy : IDocumentEvaluationPolicy {
            public DocumentEvaluationInputs Resolve(SignatureDifficultyContext difficulty) {
                return new DocumentEvaluationInputs(difficulty.EffectiveDifficulty, SignatureRuleModifiers.None);
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
