using System;
using System.Collections.Generic;
using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Bills;
using Data.Cache;
using Data.Documents;
using Data.Enums;
using Data.Formulas;
using Data.Modifiers;
using Data.Modifiers.Calculation;
using Data.Modifiers.Numeric;
using Data.Results;
using Data.Rules;
using Data.Upgrades;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using R3;
using Presentation;
using Services;
using Services.Calculators;
using Services.Locator;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;
using Utils;
using Utils.Metadata;

namespace Tests.EditMode {
    public sealed class BillSystemTests {
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
        public void DeferredRestore_AppliesAfterDefinitionsAndRejectsCompletedActiveStateDuringParse() {
            BillRewardDefinition reward = CreateReward("deferred", repeatable: true, cost: 1d);
            reward.BaseRequiredProgress = 10d;
            TestEnvironment source = CreateEnvironment(new[] { reward });
            StartBill(source, source.Bills.Catalog[0], 0.5f);
            JToken saved = source.Bills.Serialize();

            TestEnvironment restored = CreateEnvironment(
                new[] { reward },
                deferredRestore: saved);
            Assert.That(restored.Bills.ActiveBills, Has.Count.EqualTo(1));
            Assert.That(restored.Bills.ActiveBills[0].Progress, Is.Zero);

            JObject malformed = (JObject)saved.DeepClone();
            JObject active = (JObject)((JArray)malformed["active"])[0];
            active["progress"] = active["option"]["requiredProgress"].Value<double>();
            var uninitialized = new BillService(null, new BillRandom(7UL));
            Assert.Throws<Newtonsoft.Json.JsonSerializationException>(() => uninitialized.Deserialize(malformed));
            uninitialized.Dispose();
        }

        [Test]
        public void ExtremeRewardStrength_SaturatesMoneyGenerationAndCompletionModifiers() {
            PredefinedMetadataWrapperStorage.Rebuild();
            BillRewardDefinition fallback = CreateReward("fallback", repeatable: true, cost: 1d);
            BillRewardDefinition extreme = CreateReward("extreme", repeatable: true, cost: 1d);
            extreme.MinimumRequirementCount = 1;
            extreme.MaximumRequirementCount = 1;
            extreme.BaseRequiredProgress = 1d;
            extreme.BaseActiveGenerationBonus = double.MaxValue;
            extreme.MoneyReward = Value.One;
            extreme.CompletionModifiers = new[] { CreateGenerationModifier() };
            var template = TrackObject(ScriptableObject.CreateInstance<BillRequirementTemplateDefinition>());
            template.Id = "extreme_clerk";
            template.Kind = BillRequirementKind.MinimumClerkCount;
            template.MinimumTarget = 0;
            template.MaximumTarget = 0;
            template.MinimumBalance = new BillRequirementBalance {
                CostMultiplier = 1d,
                RewardFactor = double.MaxValue
            };
            template.MaximumBalance = template.MinimumBalance;
            BillEntries entries = DefaultBillEntries();
            entries.CatalogSize = 2;
            TestEnvironment environment = CreateEnvironment(
                new[] { fallback, extreme }, entries, new[] { template });

            GeneratedBillOption option = null;
            for (int index = 0; index < environment.Bills.Catalog.Count; index++) {
                if (environment.Bills.Catalog[index].Reward.Id == extreme.Id) {
                    option = environment.Bills.Catalog[index];
                    break;
                }
            }
            Assert.That(option, Is.Not.Null);
            Assert.DoesNotThrow(() => StartBill(environment, option, 1f));
            Assert.That(environment.Bills.GetStrongestActiveGenerationBonus(entries), Is.EqualTo(double.MaxValue));
            Assert.DoesNotThrow(() => environment.Accepted.Report(NormalDocumentProcessingSource.Manual, 1));
            Assert.That(environment.Bills.CompletedBills, Has.Count.EqualTo(1));
            Assert.That(environment.Wallet.CurrentBalance.Base.Degree, Is.GreaterThan(0));

            GenerationEntries modified = BillCompletionModifierEvaluator.Apply(
                new GenerationEntries(1f, 0f),
                environment.Bills.CompletedBills,
                entries);
            Assert.That(modified.TokenPerSecond, Is.EqualTo(float.MaxValue));
        }

        [Test]
        public void EmptyCatalog_StartsWithoutOptionsAndNonEmptyCatalogRequiresFallback() {
            LogAssert.Expect(LogType.Warning,
                "Bill catalog contains no rewards. The bill system started with an empty catalog.");
            TestEnvironment empty = CreateEnvironment(Array.Empty<BillRewardDefinition>());
            Assert.That(empty.Bills.Catalog, Is.Empty);

            BillRewardDefinition invalid = CreateReward("invalid", repeatable: false, cost: 1d);
            var catalog = TrackObject(ScriptableObject.CreateInstance<BillCatalogDefinition>());
            catalog.Rewards = new[] { invalid };
            var service = new BillService(null, new BillRandom(1UL));
            Assert.Throws<InvalidOperationException>(() => service.BuildDefinitions(catalog));
            service.Dispose();
        }

        [Test]
        public void QualityRequirement_UsesDisplayedLevelsAndFrozenOptionFormulas() {
            BillRewardDefinition fallback = CreateReward("fallback", repeatable: true, cost: 1d);
            BillRewardDefinition qualityReward = CreateReward("quality", repeatable: true, cost: 5d);
            qualityReward.MinimumRequirementCount = 1;
            qualityReward.MaximumRequirementCount = 1;
            qualityReward.BaseRequiredProgress = 10d;
            var template = TrackObject(ScriptableObject.CreateInstance<BillRequirementTemplateDefinition>());
            template.Id = "quality_requirement";
            template.Kind = BillRequirementKind.MinimumUnlockedDocumentQuality;
            template.MinimumTarget = 2;
            template.MaximumTarget = 3;
            template.MinimumBalance = new BillRequirementBalance {
                CostMultiplier = 2d, WorkFactor = 0.5d, RewardFactor = 0.1d, DifficultyFactor = 0.4d
            };
            template.MaximumBalance = new BillRequirementBalance {
                CostMultiplier = 3d, WorkFactor = 1d, RewardFactor = 0.2d, DifficultyFactor = 0.8d
            };
            BillEntries entries = DefaultBillEntries();
            entries.CatalogSize = 2;
            TestEnvironment environment = CreateEnvironment(
                new[] { fallback, qualityReward },
                entries,
                new[] { template },
                new DocumentEntries { DocumentQualityLevel = 1, SelectedDocumentQualityLevel = 0 });

            GeneratedBillOption option = null;
            for (int index = 0; index < environment.Bills.Catalog.Count; index++) {
                if (environment.Bills.Catalog[index].Reward.Id == qualityReward.Id) {
                    option = environment.Bills.Catalog[index];
                    break;
                }
            }
            Assert.That(option, Is.Not.Null);
            BillRequirementSnapshot requirement = option.Requirements[0];
            Assert.That(requirement.NumericTarget, Is.InRange(2, 3));
            double t = requirement.NumericTarget - 2d;
            double expectedCostMultiplier = 2d + t;
            double expectedWorkFactor = 0.5d + 0.5d * t;
            double expectedDifficulty = 0.4d + 0.4d * t;
            Assert.That(option.RawCost, Is.EqualTo(new Value(5d * expectedCostMultiplier)));
            Assert.That(option.RequiredProgress, Is.EqualTo(10d * (1d + expectedWorkFactor)).Within(0.0001d));
            Assert.That(option.SignatureThreshold,
                Is.EqualTo(0.5d + 0.05d * expectedDifficulty).Within(0.0001d));
            Assert.That(environment.Bills.AreRequirementsSatisfied(option),
                Is.EqualTo(requirement.NumericTarget <= 2));
            bool anySatisfied = false;
            for (int index = 0; index < environment.Bills.Catalog.Count; index++) {
                anySatisfied |= environment.Bills.AreRequirementsSatisfied(environment.Bills.Catalog[index]);
            }
            Assert.That(anySatisfied, Is.True);
        }

        [Test]
        public void PurchaseAndRejectedSignature_LoseMoneyAndRegenerateCatalog() {
            BillRewardDefinition reward = CreateReward("fallback", repeatable: true, cost: 10d);
            TestEnvironment environment = CreateEnvironment(new[] { reward });
            GeneratedBillOption first = environment.Bills.Catalog[0];

            Assert.That(environment.Bills.TryPurchase(first.OptionId), Is.True);
            Assert.That(environment.Wallet.CurrentBalance, Is.EqualTo(new Value(90d)));
            Assert.That(environment.Bills.Pending, Is.Not.Null);
            Assert.That(environment.Bills.Catalog, Is.Empty);

            var producer = new BillDocumentProducer();
            producer.InitializeAsync(environment.Scope).GetAwaiter().GetResult();
            Assert.That(producer.Priority, Is.EqualTo(300));
            Assert.That(producer.TryPeekOffer(out DocumentOffer offer), Is.True);
            Assert.That(producer.TryProduce(offer.Key, out IDocumentSession session), Is.True);
            var configured = new SignatureDifficultyRules("base", 0.1f, 1f, 1f, 1f, null);
            var effective = configured with { MinimumSimilarity = 0.2f, CorridorWidthMultiplier = 2f };
            DocumentEvaluationInputs inputs = session.EvaluationPolicy.Resolve(
                new SignatureDifficultyContext(configured, effective));
            Assert.That(inputs.Difficulty.MinimumSimilarity, Is.EqualTo(0.5f));
            Assert.That(inputs.Difficulty.CorridorWidthMultiplier, Is.EqualTo(1f));
            Assert.That(inputs.Modifiers.MinimumSimilarityOffset, Is.Zero);

            Assert.That(session.TryProcess(Evaluation(SignatureEvaluationStatus.Rejected, 0.2f, 0.5f)), Is.True);
            session.Dispose();
            Assert.That(environment.Bills.Pending, Is.Null);
            Assert.That(environment.Bills.ActiveBills, Is.Empty);
            Assert.That(environment.Bills.Catalog, Is.Not.Empty);
            Assert.That(environment.Wallet.CurrentBalance, Is.EqualTo(new Value(90d)));
        }

        [Test]
        public void AcceptedDocument_CompletesBillAndScalesOneTimeMoney() {
            BillRewardDefinition reward = CreateReward("reward", repeatable: true, cost: 10d);
            reward.MoneyReward = new Value(10d);
            reward.BaseRequiredProgress = 1d;
            TestEnvironment environment = CreateEnvironment(new[] { reward });

            StartBill(environment, environment.Bills.Catalog[0], 1f);
            Assert.That(environment.Bills.ActiveBills, Has.Count.EqualTo(1));
            Assert.That(environment.Bills.Catalog, Is.Empty);

            environment.Accepted.Report(NormalDocumentProcessingSource.Manual, 1);

            Assert.That(environment.Bills.ActiveBills, Is.Empty);
            Assert.That(environment.Bills.CompletedBills, Has.Count.EqualTo(1));
            Assert.That(environment.Bills.Catalog, Is.Not.Empty);
            Assert.That(environment.Wallet.CurrentBalance, Is.EqualTo(new Value(105d)));
            Assert.That(environment.Bills.CompletedBills[0].SavedBaseRewardStrength,
                Is.EqualTo(1.5d).Within(0.0001d));
        }

        [Test]
        public void WeightedRoundRobin_DistributesAcceptedDocumentsByConfiguredWeights() {
            BillRewardDefinition firstReward = CreateReward("first", repeatable: true, cost: 1d);
            BillRewardDefinition secondReward = CreateReward("second", repeatable: true, cost: 1d);
            firstReward.BaseRequiredProgress = 100d;
            secondReward.BaseRequiredProgress = 100d;
            BillEntries entries = DefaultBillEntries();
            entries.CatalogSize = 2;
            entries.ActiveProjectLimit = 2;
            TestEnvironment environment = CreateEnvironment(new[] { firstReward, secondReward }, entries);

            GeneratedBillOption first = environment.Bills.Catalog[0];
            StartBill(environment, first, 0.5f);
            GeneratedBillOption second = environment.Bills.Catalog[0];
            if (second.Reward.Id == first.Reward.Id) second = environment.Bills.Catalog[1];
            StartBill(environment, second, 0.5f);

            ActiveBillState high = environment.Bills.ActiveBills[0];
            ActiveBillState low = environment.Bills.ActiveBills[1];
            Assert.That(environment.Bills.TrySetPriorityWeight(high.InstanceId, 3), Is.True);
            Assert.That(environment.Bills.TrySetPriorityWeight(low.InstanceId, 1), Is.True);
            for (int index = 0; index < 4; index++) {
                environment.Accepted.Report(NormalDocumentProcessingSource.Manual, 1);
            }

            Assert.That(FindActive(environment.Bills, high.InstanceId).Progress, Is.EqualTo(3d));
            Assert.That(FindActive(environment.Bills, low.InstanceId).Progress, Is.EqualTo(1d));
        }

        [Test]
        public void SaveRoundTrip_PreservesPendingAndInvalidatesStaleClaim() {
            BillRewardDefinition reward = CreateReward("fallback", repeatable: true, cost: 4d);
            TestEnvironment environment = CreateEnvironment(new[] { reward });
            Assert.That(environment.Bills.TryPurchase(environment.Bills.Catalog[0].OptionId), Is.True);
            var producer = new BillDocumentProducer();
            producer.InitializeAsync(environment.Scope).GetAwaiter().GetResult();
            Assert.That(producer.TryPeekOffer(out DocumentOffer offer), Is.True);
            Assert.That(producer.TryProduce(offer.Key, out IDocumentSession stale), Is.True);
            var saved = environment.Bills.Serialize();

            environment.Bills.Deserialize(saved);

            Assert.That(stale.TryProcess(Evaluation(SignatureEvaluationStatus.Accepted, 1f, 0.5f)), Is.False);
            stale.Dispose();
            Assert.That(environment.Bills.Pending, Is.Not.Null);
            Assert.That(producer.TryPeekOffer(out _), Is.True);
        }

        [Test]
        public void ExplicitProducerRegistration_ExposesConcreteAndUiProducerContract() {
            var scope = new ServiceScope(null);
            var producer = new BillDocumentProducer();
            scope.Register(producer, typeof(IDocumentProducer));
            Track(scope);

            Assert.That(scope.Get<BillDocumentProducer>(), Is.SameAs(producer));
            Assert.That(scope.Get<IDocumentProducer>(), Is.SameAs(producer));
        }

        [Test]
        public void CompletedSaveRoundTrip_PreservesOptionWorkAndActualPayout() {
            BillRewardDefinition reward = CreateReward("history", repeatable: true, cost: 10d);
            reward.MoneyReward = new Value(4d);
            reward.BaseRequiredProgress = 1d;
            TestEnvironment source = CreateEnvironment(new[] { reward });
            StartBill(source, source.Bills.Catalog[0], 1f);
            source.Accepted.Report(NormalDocumentProcessingSource.Manual, 1);

            JToken saved = source.Bills.Serialize();
            TestEnvironment restored = CreateEnvironment(new[] { reward }, deferredRestore: saved);

            Assert.That(restored.Bills.CompletedBills, Has.Count.EqualTo(1));
            BillCompletionRecord completion = restored.Bills.CompletedBills[0];
            Assert.That(completion.Option, Is.Not.Null);
            Assert.That(completion.HasCompleteWorkStatistics, Is.True);
            Assert.That(completion.ProcessedDocumentCount, Is.EqualTo(1));
            Assert.That(completion.PaidCost, Is.EqualTo(new Value(10d)));
            Assert.That(completion.HasCompletionPayout, Is.True);
            Assert.That(completion.ActualCompletionPayout, Is.EqualTo(new Value(6d)));
        }

        [Test]
        public void LegacyCompletedSave_RemainsAvailableButMarksHistoricalStatisticsUnavailable() {
            BillRewardDefinition reward = CreateReward("legacy", repeatable: true, cost: 1d);
            TestEnvironment source = CreateEnvironment(new[] { reward });
            JObject saved = (JObject)source.Bills.Serialize();
            ((JArray)saved["catalog"]).Clear();
            saved["completed"] = new JArray(new JObject {
                ["rewardId"] = reward.Id,
                ["baseRewardStrength"] = 1d,
                ["completionOrder"] = 1L
            });
            saved["nextCompletionOrder"] = 2L;

            TestEnvironment restored = CreateEnvironment(new[] { reward }, deferredRestore: saved);

            BillCompletionRecord completion = restored.Bills.CompletedBills[0];
            Assert.That(completion.Option, Is.Null);
            Assert.That(completion.HasCompleteWorkStatistics, Is.False);
            Assert.That(completion.HasCompletionPayout, Is.False);
        }

        [Test]
        public void DynamicDescription_UsesRequirementsThenSignatureAndFrozenPayout() {
            BillRewardDefinition reward = CreateReward("description", repeatable: true, cost: 1d);
            reward.Description = "Payout ${moneyReward}; generation ${activeGeneration}.";
            reward.MoneyReward = new Value(10d);
            reward.BaseActiveGenerationBonus = 0.1d;
            reward.BaseRequiredProgress = 1d;
            TestEnvironment environment = CreateEnvironment(new[] { reward });
            GeneratedBillOption option = environment.Bills.Catalog[0];

            Assert.That(BillDescriptionFormatter.FormatCatalog(environment.Bills, option),
                Is.EqualTo("Payout 10$; generation +10%."));
            StartBill(environment, option, 1f);
            ActiveBillState active = environment.Bills.ActiveBills[0];
            Assert.That(BillDescriptionFormatter.FormatActive(environment.Bills, active),
                Is.EqualTo("Payout 15$; generation +15%."));

            environment.Accepted.Report(NormalDocumentProcessingSource.Manual, 1);
            BillCompletionRecord completed = environment.Bills.CompletedBills[0];
            Assert.That(BillDescriptionFormatter.FormatCompleted(environment.Bills, completed),
                Is.EqualTo("Payout 15$; generation +0%."));
        }

        [Test]
        public void NumericOverdrive_ExtrapolatesOnlyWhenExplicitlyEnabled() {
            Assert.That(Data.Modifiers.Calculation.NumericModifierCalculator.Apply(
                    10d,
                    Data.Modifiers.Calculation.NumericModifierOperation.Add,
                    4d,
                    1.5d),
                Is.EqualTo(14d));
            Assert.That(Data.Modifiers.Calculation.NumericModifierCalculator.Apply(
                    10d,
                    Data.Modifiers.Calculation.NumericModifierOperation.Add,
                    4d,
                    1.5d,
                    true),
                Is.EqualTo(16d));
            Assert.That(Data.Modifiers.Calculation.NumericModifierCalculator.Apply(
                    100d,
                    Data.Modifiers.Calculation.NumericModifierOperation.Multiply,
                    0d,
                    1.5d,
                    true),
                Is.EqualTo(-50d));
        }

        [Test]
        public void BankCompensation_RefundsActualPurchaseDebitWithoutChangingFrozenPrice() {
            BillRewardDefinition reward = CreateReward("bank_compensation", repeatable: true, cost: 20d);
            TestEnvironment environment = CreateEnvironment(
                new[] { reward },
                bankCompensationRatio: 0.25d);
            CompleteUpgrade(environment.Upgrades, "bank_unlock");
            Value balanceBefore = environment.Wallet.CurrentBalance;
            int walletChanges = 0;
            using IDisposable subscription = environment.Wallet.BalanceChanged.Subscribe(_ => walletChanges++);
            walletChanges = 0;

            bool purchased = environment.Bills.TryPurchase(environment.Bills.Catalog[0].OptionId);

            Assert.That(purchased, Is.True);
            Assert.That(environment.Bills.Pending, Is.Not.Null);
            Assert.That(environment.Bills.Pending.PaidCost.ToDouble(), Is.EqualTo(20d).Within(0.0001d));
            Assert.That(environment.Bills.Catalog, Is.Empty);
            Assert.That(environment.Wallet.CurrentBalance, Is.EqualTo(balanceBefore - new Value(15d)));
            Assert.That(walletChanges, Is.EqualTo(1));
        }

        private TestEnvironment CreateEnvironment(
            IReadOnlyList<BillRewardDefinition> rewards,
            BillEntries? configuredEntries = null,
            IReadOnlyList<BillRequirementTemplateDefinition> templates = null,
            DocumentEntries? configuredDocuments = null,
            JToken deferredRestore = null,
            double bankCompensationRatio = 0d) {
            var catalog = TrackObject(ScriptableObject.CreateInstance<BillCatalogDefinition>());
            catalog.Rewards = ToArray(rewards);
            catalog.RequirementTemplates = templates == null
                ? Array.Empty<BillRequirementTemplateDefinition>()
                : ToArray(templates);
            var documentReference = TrackObject(ScriptableObject.CreateInstance<DocumentReference>());
            documentReference.Value = configuredDocuments ?? new DocumentEntries {
                DocumentQualityLevel = 0,
                SelectedDocumentQualityLevel = 0
            };
            UpgradeNodeDefinition bankUnlock = CreateUpgrade("bank_unlock", FeatureIds.Bank);
            var provider = new FakeAssetProvider(catalog, documentReference, new[] { bankUnlock });
            var locatorObject = TrackObject(new GameObject("BillTestLocator", typeof(ServiceLocator)));
            var locator = locatorObject.GetComponent<ServiceLocator>();
            locator.Register<IAssetProvider>(provider);
            var scope = new ServiceScope(locator);

            var cache = new CacheVersionService();
            var wallet = new WalletService();
            wallet.ReplenishWallet(new Value(100d));
            var upgrades = new UpgradeService(provider);
            var unlocks = new UnlockService();
            var office = new OfficeService(() => 1f, Observable.Empty<float>());
            var accepted = new AcceptedNormalDocumentService();
            var storage = new ModifierStorage();
            storage.RegisterProvider(new Data.Modifiers.Providers.UpgradeModifierProvider());
            var modifierService = new ModifierService();
            var documentCalculator = new DocumentCacheCalculator();
            var stash = new PlayerStatStash();
            var bank = new BankService(null, Observable.Empty<float>());
            var bills = new BillService(provider, new BillRandom(12345UL));

            BillEntries entries = configuredEntries ?? DefaultBillEntries();
            var bankEntries = new BankEntries {
                PayoutAmount = Value.One,
                PayoutIntervalSeconds = 10f,
                CriticalChance = 0f,
                CriticalMultiplier = 2d,
                BillCostCompensationRatio = bankCompensationRatio
            };
            Register(scope, cache, wallet, upgrades, unlocks, office, accepted, storage, modifierService,
                documentCalculator, stash, bank, bills, entries, bankEntries);
            modifierService.InitializeAsync(scope).GetAwaiter().GetResult();
            upgrades.InitializeAsync(scope).GetAwaiter().GetResult();
            SetAllAvailable(upgrades);
            unlocks.InitializeAsync(scope).GetAwaiter().GetResult();
            documentCalculator.PreInitializeAsync(scope).GetAwaiter().GetResult();
            stash.PreInitializeAsync(scope).GetAwaiter().GetResult();
            bank.InitializeAsync(scope).GetAwaiter().GetResult();
            storage.PostInitializeAsync(scope).GetAwaiter().GetResult();
            if (deferredRestore != null) bills.Deserialize(deferredRestore);
            bills.InitializeAsync(scope).GetAwaiter().GetResult();
            bills.PostInitializeAsync(scope).GetAwaiter().GetResult();

            var environment = new TestEnvironment(scope, wallet, accepted, upgrades, bank, bills);
            _disposables.Add(environment);
            return environment;
        }

        private static void Register(
            ServiceScope scope,
            CacheVersionService cache,
            WalletService wallet,
            UpgradeService upgrades,
            UnlockService unlocks,
            OfficeService office,
            AcceptedNormalDocumentService accepted,
            ModifierStorage storage,
            ModifierService modifierService,
            DocumentCacheCalculator documentCalculator,
            PlayerStatStash stash,
            BankService bank,
            BillService bills,
            BillEntries entries,
            BankEntries bankEntries) {
            scope.Register(cache, typeof(ICacheVersionProvider), typeof(ICacheInvalidator));
            scope.Register(wallet);
            scope.Register(upgrades);
            scope.Register(unlocks);
            scope.Register(office);
            scope.Register(accepted);
            scope.Register(storage);
            scope.Register<Data.Modifiers.IModifierService>(modifierService);
            scope.Register(documentCalculator, typeof(ICacheCalculator<DocumentEntries>));
            scope.Register(new StaticCalculator<IncomeEntries>(new IncomeEntries(1f, 0.5f, Value.One)),
                typeof(ICacheCalculator<IncomeEntries>));
            scope.Register(new StaticCalculator<GenerationEntries>(new GenerationEntries(1f, 0f)),
                typeof(ICacheCalculator<GenerationEntries>));
            scope.Register(new StaticCalculator<SignatureEntries>(default),
                typeof(ICacheCalculator<SignatureEntries>));
            scope.Register(new StaticCalculator<OfficeEntries>(default),
                typeof(ICacheCalculator<OfficeEntries>));
            scope.Register(new StaticCalculator<BankEntries>(bankEntries),
                typeof(ICacheCalculator<BankEntries>));
            scope.Register(new StaticCalculator<BillEntries>(entries),
                typeof(ICacheCalculator<BillEntries>));
            scope.Register(stash);
            scope.Register(bank);
            scope.Register(bills);
        }

        private void StartBill(TestEnvironment environment, GeneratedBillOption option, float similarity) {
            Assert.That(environment.Bills.TryPurchase(option.OptionId), Is.True);
            var producer = new BillDocumentProducer();
            producer.InitializeAsync(environment.Scope).GetAwaiter().GetResult();
            Assert.That(producer.TryPeekOffer(out DocumentOffer offer), Is.True);
            Assert.That(producer.TryProduce(offer.Key, out IDocumentSession session), Is.True);
            Assert.That(session.TryProcess(Evaluation(
                SignatureEvaluationStatus.Accepted,
                similarity,
                environment.Bills.Pending.Option.SignatureThreshold)), Is.True);
            session.Dispose();
        }

        private BillRewardDefinition CreateReward(string id, bool repeatable, double cost) {
            var reward = TrackObject(ScriptableObject.CreateInstance<BillRewardDefinition>());
            reward.Id = id;
            reward.Name = id;
            reward.BaseCost = new Value(cost);
            reward.BaseRequiredProgress = 1d;
            reward.MinimumRequirementCount = 0;
            reward.MaximumRequirementCount = 0;
            reward.Repeatable = repeatable;
            reward.CompletionModifiers = Array.Empty<Data.Modifiers.ModifierDefinition>();
            return reward;
        }

        private ModifierDefinition CreateGenerationModifier() {
            var value = new ConstantNumericValueDefinition();
            typeof(ConstantNumericValueDefinition).GetField("_value",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(value, Value.One);
            var parameter = new CacheParameterReference();
            typeof(CacheParameterReference).GetField("_groupId",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(parameter, "Generation");
            typeof(CacheParameterReference).GetField("_parameterId",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(parameter, nameof(GenerationEntries.TokenPerSecond));
            var numeric = new NumericModifierDefinition();
            Type type = typeof(NumericModifierDefinition);
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            type.GetField("_id", flags)?.SetValue(numeric, "bill_generation_add");
            type.GetField("_operation", flags)?.SetValue(numeric, NumericModifierOperation.Add);
            type.GetField("_value", flags)?.SetValue(numeric, value);
            type.GetField("_parameter", flags)?.SetValue(numeric, parameter);
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

        private static BillEntries DefaultBillEntries() {
            return new BillEntries {
                CatalogSize = 3,
                ActiveProjectLimit = 1,
                CostMultiplier = 1f,
                OverallRewardMultiplier = 1f,
                ActiveGenerationBonusMultiplier = 1f,
                ActiveIncomePenaltyStrength = 1f,
                MaximumSignatureRewardMultiplier = 1.5f,
                RequirementRewardFactorMultiplier = 1f,
                BaseSignatureThreshold = 0.5f,
                MaximumThresholdAddition = 0.05f,
                BaseActiveIncomeMultiplier = 0.9f,
                MaximumPriorityWeight = 100
            };
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

        private static void SetAllAvailable(UpgradeService upgrades) {
            var availability = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (UpgradeNodeState state in upgrades.Nodes) availability.Add(state.Definition.Id, true);
            upgrades.ApplyAvailabilityBatch(availability);
        }

        private static void CompleteUpgrade(UpgradeService upgrades, string id) {
            Assert.That(upgrades.TryUpgrade(id), Is.True);
            Assert.That(upgrades.TryClaimPendingUpgrade(out UpgradeService.UpgradeDocumentClaim claim), Is.True);
            Assert.That(upgrades.TryCompletePendingUpgrade(claim, Evaluation(
                SignatureEvaluationStatus.Accepted,
                1f,
                0.4f)), Is.True);
        }

        private static BillRewardDefinition[] ToArray(IReadOnlyList<BillRewardDefinition> values) {
            var result = new BillRewardDefinition[values.Count];
            for (int index = 0; index < values.Count; index++) result[index] = values[index];
            return result;
        }

        private static BillRequirementTemplateDefinition[] ToArray(
            IReadOnlyList<BillRequirementTemplateDefinition> values) {
            var result = new BillRequirementTemplateDefinition[values.Count];
            for (int index = 0; index < values.Count; index++) result[index] = values[index];
            return result;
        }

        private static ActiveBillState FindActive(BillService bills, long instanceId) {
            for (int index = 0; index < bills.ActiveBills.Count; index++) {
                if (bills.ActiveBills[index].InstanceId == instanceId) return bills.ActiveBills[index];
            }
            return null;
        }

        private T TrackObject<T>(T value) where T : UnityEngine.Object {
            _objects.Add(value);
            return value;
        }

        private T Track<T>(T value) where T : IDisposable {
            _disposables.Add(value);
            return value;
        }

        private sealed class TestEnvironment : IDisposable {
            public ServiceScope Scope { get; }
            public WalletService Wallet { get; }
            public AcceptedNormalDocumentService Accepted { get; }
            public UpgradeService Upgrades { get; }
            public BankService Bank { get; }
            public BillService Bills { get; }

            public TestEnvironment(
                ServiceScope scope,
                WalletService wallet,
                AcceptedNormalDocumentService accepted,
                UpgradeService upgrades,
                BankService bank,
                BillService bills) {
                Scope = scope;
                Wallet = wallet;
                Accepted = accepted;
                Upgrades = upgrades;
                Bank = bank;
                Bills = bills;
            }

            public void Dispose() => Scope.Dispose();
        }

        private sealed class StaticCalculator<T> : ICacheCalculator<T>, IService where T : struct {
            private readonly T _value;
            public StaticCalculator(T value) => _value = value;
            public T Calculate() => _value;
            public void Dispose() { }
        }

        private sealed class FakeAssetProvider : IAssetProvider, IService {
            private readonly BillCatalogDefinition _catalog;
            private readonly DocumentReference _documentReference;
            private readonly IReadOnlyList<UpgradeNodeDefinition> _upgrades;

            public FakeAssetProvider(BillCatalogDefinition catalog, DocumentReference documentReference,
                IReadOnlyList<UpgradeNodeDefinition> upgrades) {
                _catalog = catalog;
                _documentReference = documentReference;
                _upgrades = upgrades;
            }

            public UniTask<IAssetLease<T>> LoadAsync<T>(AssetReferenceT<T> reference) where T : UnityEngine.Object {
                throw new NotSupportedException();
            }

            public UniTask<IAssetListLease<T>> LoadAssetsByLabelAsync<T>(string label)
                where T : UnityEngine.Object {
                if (typeof(T) == typeof(BillCatalogDefinition)) {
                    return UniTask.FromResult<IAssetListLease<T>>(
                        new FakeAssetListLease<T>(new[] { (T)(object)_catalog }));
                }
                if (typeof(T) == typeof(DocumentReference)) {
                    return UniTask.FromResult<IAssetListLease<T>>(
                        new FakeAssetListLease<T>(new[] { (T)(object)_documentReference }));
                }
                if (typeof(T) == typeof(Data.Upgrades.UpgradeNodeDefinition)) {
                    var upgrades = new T[_upgrades.Count];
                    for (int index = 0; index < upgrades.Length; index++) {
                        upgrades[index] = (T)(object)_upgrades[index];
                    }
                    return UniTask.FromResult<IAssetListLease<T>>(
                        new FakeAssetListLease<T>(upgrades));
                }
                throw new NotSupportedException(typeof(T).FullName);
            }

            public UniTask<IInstanceLease> InstantiateAsync(
                AssetReference instanceReference,
                Transform parent = null,
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
