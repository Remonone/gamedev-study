using System;
using System.Collections.Generic;
using System.Reflection;
using Bootstrap.Installer;
using Constants;
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
using Data.Research;
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
using Utils.Metadata;

namespace Tests.EditMode {
    public sealed class ResearchSystemTests {
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
        public void LockedDocumentsDoNotProgress_UnlockStartsResearchAndOffers() {
            PracticeDefinition practice = CreateMoneyPractice("money", "common", 1d, 0.5f);
            TestEnvironment environment = CreateEnvironment(new[] { practice }, DefaultRarities());

            environment.Accepted.Report(NormalDocumentProcessingSource.Manual, 1, 1f);
            Assert.That(environment.Research.Progress, Is.Zero);
            Assert.That(environment.Research.CurrentOffers, Is.Empty);

            UnlockArchive(environment);
            environment.Accepted.Report(NormalDocumentProcessingSource.Manual, 1, 1f);

            Assert.That(environment.Research.Progress, Is.EqualTo(1d));
            Assert.That(environment.Research.CurrentOffers, Has.Count.EqualTo(1));
        }

        [Test]
        public void LockedAndNoEligibleStatesDoNotConsumeResearchRandom() {
            TestEnvironment environment = CreateEnvironment(Array.Empty<PracticeDefinition>(), Array.Empty<PracticeRarityDefinition>());
            ulong before = environment.Research.RandomState;
            environment.Accepted.Report(NormalDocumentProcessingSource.Manual, 1, 1f);
            Assert.That(environment.Research.RandomState, Is.EqualTo(before));

            UnlockArchive(environment);
            before = environment.Research.RandomState;
            environment.Accepted.Report(NormalDocumentProcessingSource.Manual, 1, 1f);
            Assert.That(environment.Research.Progress, Is.EqualTo(environment.Research.RequiredPoints));
            Assert.That(environment.Research.CurrentOffers, Is.Empty);
            Assert.That(environment.Research.RandomState, Is.EqualTo(before));
            environment.Accepted.Report(NormalDocumentProcessingSource.Manual, 1, 1f);
            Assert.That(environment.Research.RandomState, Is.EqualTo(before));
        }

        [Test]
        public void LockedRestoreKeepsDormantStatesAndReconcilesCollectingOnUnlock() {
            PracticeDefinition practice = CreateMoneyPractice("money", "common", 1d, 0.5f);
            var collectingSave = new JObject {
                ["progress"] = 1d,
                ["resolvedCycles"] = 0L,
                ["rngState"] = "1234",
                ["offers"] = new JArray(),
                ["pending"] = JValue.CreateNull(),
                ["active"] = new JArray()
            };
            TestEnvironment collecting = CreateEnvironment(
                new[] { practice }, DefaultRarities(), deferredRestore: collectingSave);
            Assert.That(collecting.Research.CurrentOffers, Is.Empty);
            UnlockArchive(collecting);
            Assert.That(collecting.Research.CurrentOffers, Has.Count.EqualTo(1));

            TestEnvironment source = CreateEnvironment(new[] { practice }, DefaultRarities());
            UnlockArchive(source);
            source.Accepted.Report(NormalDocumentProcessingSource.Manual, 1, 1f);
            JToken offeredSave = source.Research.Serialize();
            TestEnvironment offered = CreateEnvironment(new[] { practice }, DefaultRarities(), deferredRestore: offeredSave);
            Assert.That(offered.Research.CurrentOffers, Has.Count.EqualTo(1));
            Assert.That(offered.Research.TrySellOffer(), Is.False);
            UnlockArchive(offered);
            Assert.That(offered.Research.CurrentOffers, Has.Count.EqualTo(1));

            Assert.That(source.Research.TrySelectPractice("money"), Is.True);
            JToken pendingSave = source.Research.Serialize();
            TestEnvironment pending = CreateEnvironment(new[] { practice }, DefaultRarities(), deferredRestore: pendingSave);
            PracticeDocumentProducer producer = InitializeProducer(pending);
            Assert.That(producer.TryPeekOffer(out _), Is.False);
            UnlockArchive(pending);
            Assert.That(producer.TryPeekOffer(out _), Is.True);
        }

        [Test]
        public void LoweredRequiredPointsReconcilesWithoutDocumentOrReroll() {
            PracticeDefinition practice = CreateMoneyPractice("money", "common", 1d, 0.5f);
            ResearchEntries entries = DefaultEntries();
            entries.BaseRequiredPoints = 10d;
            TestEnvironment environment = CreateEnvironment(new[] { practice }, DefaultRarities(), entries);
            UnlockArchive(environment);
            for (int index = 0; index < 5; index++) environment.Accepted.Report(NormalDocumentProcessingSource.Manual, 1, 0f);
            Assert.That(environment.Research.CurrentOffers, Is.Empty);
            entries.BaseRequiredPoints = 5d;
            environment.ResearchCalculator.Value = entries;

            ((ICacheInvalidator)environment.Cache).Invalidate(typeof(ResearchEntries));

            Assert.That(environment.Research.CurrentOffers, Has.Count.EqualTo(1));
        }

        [Test]
        public void AcceptedDocumentUsesProcessingQualityForDeterministicDouble() {
            PracticeDefinition practice = CreateMoneyPractice("money", "common", 1d, 0.5f);
            ResearchEntries entries = DefaultEntries();
            entries.BaseRequiredPoints = 10d;
            entries.DoublePointQualityThreshold = 0.8f;
            entries.DoublePointChance = 1f;
            TestEnvironment environment = CreateEnvironment(new[] { practice }, DefaultRarities(), entries);
            UnlockArchive(environment);

            environment.Accepted.Report(NormalDocumentProcessingSource.Office, 1, 0.7f);
            environment.Accepted.Report(NormalDocumentProcessingSource.Manual, 1, 0.9f);

            Assert.That(environment.Research.Progress, Is.EqualTo(3d));
        }

        [Test]
        public void SellOfferCreditsMeanRarityPriceOnceAndAdvancesCycle() {
            PracticeRarityDefinition[] rarities = {
                Rarity("a", 1, 10d), Rarity("b", 1, 20d), Rarity("c", 1, 60d)
            };
            PracticeDefinition[] practices = {
                CreateMoneyPractice("a1", "a", 1d, 0.5f),
                CreateMoneyPractice("b1", "b", 1d, 0.5f),
                CreateMoneyPractice("c1", "c", 1d, 0.5f)
            };
            ResearchEntries entries = DefaultEntries();
            entries.OfferCount = 3;
            TestEnvironment environment = CreateEnvironment(practices, rarities, entries);
            UnlockArchive(environment);
            environment.Accepted.Report(NormalDocumentProcessingSource.Manual, 1, 1f);
            int walletEvents = 0;
            using IDisposable subscription = environment.Wallet.BalanceChanged.Subscribe(_ => walletEvents++);
            walletEvents = 0;
            Value before = environment.Wallet.CurrentBalance;

            Assert.That(environment.Research.TrySellOffer(), Is.True);

            Assert.That(environment.Wallet.CurrentBalance, Is.EqualTo(before + new Value(30d)));
            Assert.That(walletEvents, Is.EqualTo(1));
            Assert.That(environment.Research.ResolvedCycles, Is.EqualTo(1));
            Assert.That(environment.Research.Progress, Is.Zero);
        }

        [Test]
        public void AcceptedInstantPracticeUsesFrozenThresholdOverdriveAndResolvesZeroSafely() {
            PracticeDefinition practice = CreateMoneyPractice("money", "common", 10d, 0.5f);
            TestEnvironment environment = CreateEnvironment(new[] { practice }, DefaultRarities());
            UnlockArchive(environment);
            SelectOnlyOffer(environment);
            var producer = InitializeProducer(environment);
            Assert.That(producer.TryPeekOffer(out DocumentOffer offer), Is.True);
            Assert.That(producer.TryProduce(offer.Key, out IDocumentSession session), Is.True);
            Value before = environment.Wallet.CurrentBalance;

            Assert.That(session.TryProcess(Evaluation(SignatureEvaluationStatus.Accepted, 1f, 0.5f)), Is.True);

            Assert.That(environment.Wallet.CurrentBalance, Is.EqualTo(before + new Value(20d)));
            Assert.That(environment.Research.Pending, Is.Null);
            Assert.That(environment.Research.ResolvedCycles, Is.EqualTo(1));
            session.Dispose();

            environment.Accepted.Report(NormalDocumentProcessingSource.Manual, 1, 1f);
            SelectOnlyOffer(environment);
            Assert.That(producer.TryPeekOffer(out offer), Is.True);
            Assert.That(producer.TryProduce(offer.Key, out session), Is.True);
            Assert.That(session.TryProcess(Evaluation(SignatureEvaluationStatus.InvalidAttempt, 0f, 0f)), Is.True);
            Assert.That(environment.Research.Pending, Is.Null);
            Assert.That(environment.Research.ResolvedCycles, Is.EqualTo(2));
            session.Dispose();
        }

        [Test]
        public void RejectedModifierIsTimedExcludedAndEligibleImmediatelyAfterExpiry() {
            PracticeDefinition practice = CreateModifierPractice("generation", "common", 0.5f, 2f);
            TestEnvironment environment = CreateEnvironment(new[] { practice }, DefaultRarities());
            UnlockArchive(environment);
            SelectOnlyOffer(environment);
            PracticeDocumentProducer producer = InitializeProducer(environment);
            producer.TryPeekOffer(out DocumentOffer offer);
            producer.TryProduce(offer.Key, out IDocumentSession session);

            Assert.That(session.TryProcess(Evaluation(SignatureEvaluationStatus.Rejected, 0.25f, 0.5f)), Is.True);
            Assert.That(environment.Research.ActivePractices, Has.Count.EqualTo(1));
            GenerationEntries modified = environment.Storage.GetProvider<PracticeModifierProvider>()
                .Collect(new GenerationEntries(1f, 1));
            Assert.That(modified.TokenPerSecond, Is.EqualTo(2f));

            environment.Accepted.Report(NormalDocumentProcessingSource.Manual, 1, 1f);
            Assert.That(environment.Research.CurrentOffers, Is.Empty);
            environment.Research.Tick(2f);
            Assert.That(environment.Research.ActivePractices, Is.Empty);
            Assert.That(environment.Research.CurrentOffers, Has.Count.EqualTo(1));
            session.Dispose();
        }

        [Test]
        public void TimedPracticeNotifiesOnlyOnWholeSecondBoundariesAndExpiry() {
            PracticeDefinition practice = CreateModifierPractice("generation", "common", 0.5f, 2.5f);
            TestEnvironment environment = CreateEnvironment(new[] { practice }, DefaultRarities());
            UnlockArchive(environment);
            SelectOnlyOffer(environment);
            PracticeDocumentProducer producer = InitializeProducer(environment);
            producer.TryPeekOffer(out DocumentOffer offer);
            producer.TryProduce(offer.Key, out IDocumentSession session);
            session.TryProcess(Evaluation(SignatureEvaluationStatus.Rejected, 0.25f, 0.5f));
            int changes = 0;
            using IDisposable subscription = environment.Research.Changed.Subscribe(_ => changes++);

            environment.Research.Tick(0.1f);
            environment.Research.Tick(0.1f);
            environment.Research.Tick(0.1f);
            Assert.That(changes, Is.Zero);
            environment.Research.Tick(0.3f);
            Assert.That(changes, Is.EqualTo(1));
            session.Dispose();
        }

        [Test]
        public void RestoredActivePracticeIsDormantAndTimerIsPausedWhileLocked() {
            PracticeDefinition practice = CreateModifierPractice("generation", "common", 0.5f, 10f);
            TestEnvironment source = CreateEnvironment(new[] { practice }, DefaultRarities());
            UnlockArchive(source);
            SelectOnlyOffer(source);
            PracticeDocumentProducer sourceProducer = InitializeProducer(source);
            sourceProducer.TryPeekOffer(out DocumentOffer sourceOffer);
            sourceProducer.TryProduce(sourceOffer.Key, out IDocumentSession sourceSession);
            sourceSession.TryProcess(Evaluation(SignatureEvaluationStatus.Rejected, 0.25f, 0.5f));
            JToken saved = source.Research.Serialize();

            TestEnvironment restored = CreateEnvironment(
                new[] { practice }, DefaultRarities(), deferredRestore: saved);
            Assert.That(restored.Research.IsUnlocked, Is.False);
            Assert.That(restored.Research.ActivePractices, Has.Count.EqualTo(1));
            double remaining = restored.Research.ActivePractices[0].RemainingSeconds;
            GenerationEntries dormant = restored.Storage.GetProvider<PracticeModifierProvider>()
                .Collect(new GenerationEntries(1f, 1));
            Assert.That(dormant.TokenPerSecond, Is.EqualTo(1f));
            restored.Research.Tick(5f);
            Assert.That(restored.Research.ActivePractices[0].RemainingSeconds, Is.EqualTo(remaining));

            UnlockArchive(restored);
            GenerationEntries active = restored.Storage.GetProvider<PracticeModifierProvider>()
                .Collect(new GenerationEntries(1f, 1));
            Assert.That(active.TokenPerSecond, Is.EqualTo(2f));
            sourceSession.Dispose();
        }

        [Test]
        public void CatalogRejectsUndefinedEffectAndModifierOperation() {
            PracticeDefinition practice = CreateMoneyPractice("bad", "common", 1d, 0.5f);
            practice.EffectKind = (PracticeEffectKind)999;
            var catalog = TrackObject(ScriptableObject.CreateInstance<ResearchCatalogDefinition>());
            catalog.Rarities = DefaultRarities();
            catalog.Practices = new[] { practice };
            var service = new ResearchService(null, new ResearchRandom(1UL), Observable.Empty<float>());
            Assert.Throws<InvalidOperationException>(() => service.BuildDefinitions(catalog));
            service.Dispose();

            PracticeDefinition modifierPractice = CreateModifierPractice("bad_operation", "common", 0.5f, 1f);
            SetPrivate(modifierPractice.Modifiers[0].NumericModifiers[0], "_operation", (NumericModifierOperation)999);
            catalog.Practices = new[] { modifierPractice };
            service = new ResearchService(null, new ResearchRandom(1UL), Observable.Empty<float>());
            Assert.Throws<InvalidOperationException>(() => service.BuildDefinitions(catalog));
            service.Dispose();
        }

        [Test]
        public void MalformedTimedPracticeStrengthRejectsSaveAtomically() {
            var malformed = new JObject {
                ["progress"] = 0d,
                ["resolvedCycles"] = 0L,
                ["rngState"] = "1",
                ["offers"] = new JArray(),
                ["pending"] = JValue.CreateNull(),
                ["active"] = new JArray(new JObject {
                    ["practiceId"] = "generation",
                    ["effectiveness"] = 2d,
                    ["permanent"] = false,
                    ["remainingSeconds"] = 10d
                })
            };
            var service = new ResearchService(null, new ResearchRandom(1UL), Observable.Empty<float>());
            Assert.Throws<Newtonsoft.Json.JsonSerializationException>(() => service.Deserialize(malformed));
            Assert.That(service.Progress, Is.Zero);
            Assert.That(service.ActivePractices, Is.Empty);
            service.Dispose();
        }

        [Test]
        public void PendingSaveKeepsFrozenThresholdWhenDefinitionChangesAndInvalidatesStaleClaim() {
            PracticeDefinition sourcePractice = CreateMoneyPractice("money", "common", 10d, 0.5f);
            TestEnvironment source = CreateEnvironment(new[] { sourcePractice }, DefaultRarities());
            UnlockArchive(source);
            SelectOnlyOffer(source);
            PracticeDocumentProducer sourceProducer = InitializeProducer(source);
            sourceProducer.TryPeekOffer(out DocumentOffer sourceOffer);
            sourceProducer.TryProduce(sourceOffer.Key, out IDocumentSession staleSession);
            JToken saved = source.Research.Serialize();
            source.Research.Deserialize(saved);
            Assert.That(staleSession.TryProcess(Evaluation(SignatureEvaluationStatus.Accepted, 1f, 0.5f)), Is.False);
            staleSession.Dispose();

            PracticeDefinition restoredPractice = CreateMoneyPractice("money", "common", 10d, 0.8f);
            TestEnvironment restored = CreateEnvironment(new[] { restoredPractice }, DefaultRarities(), deferredRestore: saved);
            UnlockArchive(restored);
            PracticeDocumentProducer restoredProducer = InitializeProducer(restored);
            Assert.That(restoredProducer.TryPeekOffer(out DocumentOffer offer), Is.True);
            Assert.That(restoredProducer.TryProduce(offer.Key, out IDocumentSession session), Is.True);
            var configured = new Data.Rules.SignatureDifficultyRules("base", 0.1f, 1f, 1f, 1f, null);
            var effective = configured with { MinimumSimilarity = 0.2f, CorridorWidthMultiplier = 2f };
            var inputs = session.EvaluationPolicy.Resolve(
                new SignatureDifficultyContext(configured, effective));
            Assert.That(inputs.Difficulty.MinimumSimilarity, Is.EqualTo(0.5f));
            Assert.That(inputs.Difficulty.CorridorWidthMultiplier, Is.EqualTo(1f));
            Assert.That(session.TryProcess(Evaluation(SignatureEvaluationStatus.Accepted, 1f, 0.5f)), Is.True);
            Assert.That(restored.Wallet.CurrentBalance, Is.EqualTo(new Value(119d)));
            session.Dispose();
        }

        [Test]
        public void ResearchViewModelExposesCommandsAndDisposesSubscriptions() {
            PracticeDefinition practice = CreateMoneyPractice("money", "common", 1d, 0.5f);
            TestEnvironment environment = CreateEnvironment(new[] { practice }, DefaultRarities());
            UnlockArchive(environment);
            environment.Accepted.Report(NormalDocumentProcessingSource.Manual, 1, 1f);
            var viewModel = new ResearchViewModel(environment.Research);
            int changes = 0;
            using IDisposable subscription = viewModel.Changed.Subscribe(_ => changes++);

            Assert.That(viewModel.Offers, Has.Count.EqualTo(1));
            Assert.That(viewModel.SelectPractice("money"), Is.True);
            Assert.That(viewModel.HasPendingSignature, Is.True);
            Assert.That(changes, Is.GreaterThan(0));
            Assert.DoesNotThrow(viewModel.Dispose);
        }

        [Test]
        public void ModifierStorageUsesRegistrationOrderForNonCommutativeProviders() {
            var storage = new ModifierStorage();
            storage.RegisterProvider(new AddProvider(2f));
            storage.RegisterProvider(new MultiplyProvider(3f));
            var service = new ModifierService();
            var scope = new ServiceScope(null);
            scope.Register(storage);
            scope.Register<Data.Modifiers.IModifierService>(service);
            service.InitializeAsync(scope).GetAwaiter().GetResult();

            Assert.That(service.Apply(1f), Is.EqualTo(9f));
            scope.Dispose();
        }

        [Test]
        public void GameInstallerRegistersPracticeBeforeBillForMatchingBaselineOrder() {
            var locatorObject = TrackObject(new GameObject(
                "InstallerOrder", typeof(ServiceLocator), typeof(GameSceneInstaller)));
            var locator = locatorObject.GetComponent<ServiceLocator>();
            var session = new GameSessionService();
            session.Prepare(GameLaunchMode.NewGame);
            locator.Register(session);
            locatorObject.GetComponent<GameSceneInstaller>().Install(locator);
            IReadOnlyList<IModifierProvider> providers = locator.Get<ModifierStorage>().Providers;

            Assert.That(providers, Has.Count.EqualTo(4));
            Assert.That(providers[0], Is.TypeOf<UpgradeModifierProvider>());
            Assert.That(providers[1], Is.TypeOf<MetaUpgradeModifierProvider>());
            Assert.That(providers[2], Is.TypeOf<PracticeModifierProvider>());
            Assert.That(providers[3], Is.TypeOf<BillModifierProvider>());
        }

        private TestEnvironment CreateEnvironment(
            IReadOnlyList<PracticeDefinition> practices,
            IReadOnlyList<PracticeRarityDefinition> rarities,
            ResearchEntries? configuredEntries = null,
            JToken deferredRestore = null) {
            PredefinedMetadataWrapperStorage.Rebuild();
            var catalog = TrackObject(ScriptableObject.CreateInstance<ResearchCatalogDefinition>());
            catalog.Practices = ToArray(practices);
            catalog.Rarities = ToArray(rarities);
            UpgradeNodeDefinition archiveUnlock = CreateUpgrade("archive_unlock", FeatureIds.Archive);
            var provider = new FakeAssetProvider(catalog, new[] { archiveUnlock });
            var locatorObject = TrackObject(new GameObject("ResearchTestLocator", typeof(ServiceLocator)));
            var locator = locatorObject.GetComponent<ServiceLocator>();
            locator.Register<IAssetProvider>(provider);
            var scope = new ServiceScope(locator);
            var cache = new CacheVersionService();
            var wallet = new WalletService();
            wallet.ReplenishWallet(new Value(100d));
            var upgrades = new UpgradeService(provider);
            var unlocks = new UnlockService();
            var accepted = new AcceptedNormalDocumentService();
            var storage = new ModifierStorage();
            storage.RegisterProvider(new UpgradeModifierProvider());
            storage.RegisterProvider(new PracticeModifierProvider());
            var modifierService = new ModifierService();
            var stash = new PlayerStatStash();
            var research = new ResearchService(provider, new ResearchRandom(1234UL), Observable.Empty<float>());
            var researchCalculator = new MutableCalculator<ResearchEntries>(configuredEntries ?? DefaultEntries());
            Register(scope, cache, wallet, upgrades, unlocks, accepted, storage, modifierService, stash, research,
                researchCalculator);
            modifierService.InitializeAsync(scope).GetAwaiter().GetResult();
            upgrades.InitializeAsync(scope).GetAwaiter().GetResult();
            SetAllAvailable(upgrades);
            unlocks.InitializeAsync(scope).GetAwaiter().GetResult();
            stash.PreInitializeAsync(scope).GetAwaiter().GetResult();
            if (deferredRestore != null) research.Deserialize(deferredRestore);
            research.InitializeAsync(scope).GetAwaiter().GetResult();
            storage.PostInitializeAsync(scope).GetAwaiter().GetResult();
            research.PostInitializeAsync(scope).GetAwaiter().GetResult();
            var environment = new TestEnvironment(
                scope, cache, wallet, upgrades, accepted, storage, research, researchCalculator);
            _disposables.Add(environment);
            return environment;
        }

        private static void Register(
            ServiceScope scope,
            CacheVersionService cache,
            WalletService wallet,
            UpgradeService upgrades,
            UnlockService unlocks,
            AcceptedNormalDocumentService accepted,
            ModifierStorage storage,
            ModifierService modifierService,
            PlayerStatStash stash,
            ResearchService research,
            MutableCalculator<ResearchEntries> researchCalculator) {
            scope.Register(cache, typeof(ICacheVersionProvider), typeof(ICacheInvalidator));
            scope.Register(wallet);
            scope.Register(upgrades);
            scope.Register(unlocks);
            scope.Register(accepted);
            scope.Register(storage);
            scope.Register<Data.Modifiers.IModifierService>(modifierService);
            scope.Register(new StaticCalculator<IncomeEntries>(new IncomeEntries(1f, 0.5f, Value.One)), typeof(ICacheCalculator<IncomeEntries>));
            scope.Register(new StaticCalculator<GenerationEntries>(new GenerationEntries(1f, 1)), typeof(ICacheCalculator<GenerationEntries>));
            scope.Register(new StaticCalculator<SignatureEntries>(default), typeof(ICacheCalculator<SignatureEntries>));
            scope.Register(new StaticCalculator<OfficeEntries>(default), typeof(ICacheCalculator<OfficeEntries>));
            scope.Register(new StaticCalculator<DocumentEntries>(new DocumentEntries()), typeof(ICacheCalculator<DocumentEntries>));
            scope.Register(researchCalculator, typeof(ICacheCalculator<ResearchEntries>));
            scope.Register(stash);
            scope.Register(research);
        }

        private void UnlockArchive(TestEnvironment environment) {
            Assert.That(environment.Upgrades.TryUpgrade("archive_unlock"), Is.True);
            Assert.That(environment.Upgrades.TryClaimPendingUpgrade(out UpgradeService.UpgradeDocumentClaim claim), Is.True);
            Assert.That(environment.Upgrades.TryCompletePendingUpgrade(
                claim,
                Evaluation(SignatureEvaluationStatus.Accepted, 1f, 0.4f)), Is.True);
            Assert.That(environment.Research.IsUnlocked, Is.True);
        }

        private static void SelectOnlyOffer(TestEnvironment environment) {
            environment.Accepted.Report(NormalDocumentProcessingSource.Manual, 1, 1f);
            Assert.That(environment.Research.CurrentOffers, Has.Count.EqualTo(1));
            Assert.That(environment.Research.TrySelectPractice(environment.Research.CurrentOffers[0].Id), Is.True);
        }

        private static PracticeDocumentProducer InitializeProducer(TestEnvironment environment) {
            var producer = new PracticeDocumentProducer();
            producer.InitializeAsync(environment.Scope).GetAwaiter().GetResult();
            return producer;
        }

        private PracticeDefinition CreateMoneyPractice(string id, string rarityId, double payout, float threshold) {
            var definition = TrackObject(ScriptableObject.CreateInstance<PracticeDefinition>());
            definition.Id = id;
            definition.DisplayName = id;
            definition.RarityId = rarityId;
            definition.SignatureThreshold = threshold;
            definition.EffectKind = PracticeEffectKind.InstantMoney;
            definition.InstantMoney = new Value(payout);
            definition.Modifiers = Array.Empty<ModifierDefinition>();
            return definition;
        }

        private PracticeDefinition CreateModifierPractice(
            string id, string rarityId, float threshold, float duration) {
            var definition = TrackObject(ScriptableObject.CreateInstance<PracticeDefinition>());
            definition.Id = id;
            definition.DisplayName = id;
            definition.RarityId = rarityId;
            definition.SignatureThreshold = threshold;
            definition.EffectKind = PracticeEffectKind.NumericModifiers;
            definition.FailedSignatureDurationSeconds = duration;
            definition.Modifiers = new[] { CreateGenerationModifier() };
            return definition;
        }

        private ModifierDefinition CreateGenerationModifier() {
            var value = new ConstantNumericValueDefinition();
            SetPrivate(value, "_value", Value.One);
            var parameter = new CacheParameterReference();
            SetPrivate(parameter, "_groupId", "Generation");
            SetPrivate(parameter, "_parameterId", nameof(GenerationEntries.TokenPerSecond));
            var numeric = new NumericModifierDefinition();
            SetPrivate(numeric, "_id", "practice_generation_add");
            SetPrivate(numeric, "_operation", NumericModifierOperation.Add);
            SetPrivate(numeric, "_value", value);
            SetPrivate(numeric, "_parameter", parameter);
            var definition = TrackObject(ScriptableObject.CreateInstance<ModifierDefinition>());
            definition.NumericModifiers = new List<NumericModifierDefinition> { numeric };
            return definition;
        }

        private UpgradeNodeDefinition CreateUpgrade(string id, params string[] featureIds) {
            var definition = TrackObject(ScriptableObject.CreateInstance<UpgradeNodeDefinition>());
            definition.Id = id;
            definition.Name = id;
            definition.MaxLevel = 1;
            definition.CostFormula = new ConstantValue { Value = Value.One };
            definition.Modifiers = Array.Empty<ModifierDefinition>();
            definition.FeatureUnlockIds = featureIds;
            definition.StatisticRequirements = Array.Empty<GameStatisticRequirement>();
            definition.Children = Array.Empty<UpgradeNodeLink>();
            return definition;
        }

        private static ResearchEntries DefaultEntries() => new() {
            PointsPerAcceptedDocument = 1d,
            DoublePointQualityThreshold = 1f,
            DoublePointChance = 0f,
            BaseRequiredPoints = 1d,
            AdditionalRequiredPointsPerResolvedCycle = 0d,
            OfferCount = 3
        };

        private static PracticeRarityDefinition[] DefaultRarities() => new[] { Rarity("common", 1, 1d) };
        private static PracticeRarityDefinition Rarity(string id, int weight, double price) => new() {
            Id = id,
            DisplayName = id,
            Color = Color.white,
            SelectionWeight = weight,
            SalePrice = new Value(price)
        };

        private static SignatureEvaluationResult Evaluation(SignatureEvaluationStatus status, float similarity, float minimum) {
            return new SignatureEvaluationResult(
                status,
                status == SignatureEvaluationStatus.Accepted ? SignatureFailureReason.None : SignatureFailureReason.BelowSimilarityThreshold,
                similarity,
                minimum,
                null);
        }

        private static void SetAllAvailable(UpgradeService upgrades) {
            var availability = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (UpgradeNodeState state in upgrades.Nodes) availability.Add(state.Definition.Id, true);
            upgrades.ApplyAvailabilityBatch(availability);
        }

        private static void SetPrivate(object target, string field, object value) {
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
        }

        private static PracticeDefinition[] ToArray(IReadOnlyList<PracticeDefinition> values) {
            var result = new PracticeDefinition[values.Count];
            for (int index = 0; index < values.Count; index++) result[index] = values[index];
            return result;
        }

        private static PracticeRarityDefinition[] ToArray(IReadOnlyList<PracticeRarityDefinition> values) {
            var result = new PracticeRarityDefinition[values.Count];
            for (int index = 0; index < values.Count; index++) result[index] = values[index];
            return result;
        }

        private T TrackObject<T>(T value) where T : UnityEngine.Object { _objects.Add(value); return value; }

        private sealed class TestEnvironment : IDisposable {
            public ServiceScope Scope { get; }
            public CacheVersionService Cache { get; }
            public WalletService Wallet { get; }
            public UpgradeService Upgrades { get; }
            public AcceptedNormalDocumentService Accepted { get; }
            public ModifierStorage Storage { get; }
            public ResearchService Research { get; }
            public MutableCalculator<ResearchEntries> ResearchCalculator { get; }
            public TestEnvironment(ServiceScope scope, CacheVersionService cache, WalletService wallet,
                UpgradeService upgrades, AcceptedNormalDocumentService accepted, ModifierStorage storage,
                ResearchService research, MutableCalculator<ResearchEntries> researchCalculator) {
                Scope = scope;
                Cache = cache;
                Wallet = wallet;
                Upgrades = upgrades;
                Accepted = accepted;
                Storage = storage;
                Research = research;
                ResearchCalculator = researchCalculator;
            }
            public void Dispose() => Scope.Dispose();
        }

        private sealed class StaticCalculator<T> : ICacheCalculator<T>, IService where T : struct {
            private readonly T _value;
            public StaticCalculator(T value) => _value = value;
            public T Calculate() => _value;
            public void Dispose() { }
        }

        private sealed class MutableCalculator<T> : ICacheCalculator<T>, IService where T : struct {
            public T Value { get; set; }
            public MutableCalculator(T value) => Value = value;
            public T Calculate() => Value;
            public void Dispose() { }
        }

        private sealed class FakeAssetProvider : IAssetProvider, IService {
            private readonly ResearchCatalogDefinition _catalog;
            private readonly IReadOnlyList<UpgradeNodeDefinition> _upgrades;
            public FakeAssetProvider(ResearchCatalogDefinition catalog, IReadOnlyList<UpgradeNodeDefinition> upgrades) {
                _catalog = catalog;
                _upgrades = upgrades;
            }
            public UniTask<IAssetLease<T>> LoadAsync<T>(AssetReferenceT<T> reference) where T : UnityEngine.Object => throw new NotSupportedException();
            public UniTask<IAssetListLease<T>> LoadAssetsByLabelAsync<T>(string label) where T : UnityEngine.Object {
                if (typeof(T) == typeof(ResearchCatalogDefinition)) {
                    return UniTask.FromResult<IAssetListLease<T>>(new FakeAssetListLease<T>(new[] { (T)(object)_catalog }));
                }
                if (typeof(T) == typeof(UpgradeNodeDefinition)) {
                    var result = new T[_upgrades.Count];
                    for (int index = 0; index < result.Length; index++) result[index] = (T)(object)_upgrades[index];
                    return UniTask.FromResult<IAssetListLease<T>>(new FakeAssetListLease<T>(result));
                }
                throw new NotSupportedException(typeof(T).FullName);
            }
            public UniTask<IInstanceLease> InstantiateAsync(AssetReference reference, Transform parent = null, bool worldPositionStays = false) => throw new NotSupportedException();
            public void Dispose() { }
        }

        private sealed class FakeAssetListLease<T> : IAssetListLease<T> where T : UnityEngine.Object {
            public IReadOnlyList<T> Assets { get; }
            public FakeAssetListLease(IReadOnlyList<T> assets) => Assets = assets;
            public void Dispose() { }
        }

        private sealed class AddProvider : IModifierProvider {
            private readonly float _value;
            public AddProvider(float value) => _value = value;
            public T Collect<T>(T target) where T : struct => typeof(T) == typeof(float) ? (T)(object)((float)(object)target + _value) : target;
            public void Init(IServiceScope scope) { }
        }

        private sealed class MultiplyProvider : IModifierProvider {
            private readonly float _value;
            public MultiplyProvider(float value) => _value = value;
            public T Collect<T>(T target) where T : struct => typeof(T) == typeof(float) ? (T)(object)((float)(object)target * _value) : target;
            public void Init(IServiceScope scope) { }
        }
    }
}
