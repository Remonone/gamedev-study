using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Documents;
using Data.Formulas;
using Data.Modifiers;
using Data.Modifiers.Calculation;
using Data.Modifiers.Numeric;
using Data.Modifiers.Providers;
using Data.Office;
using Data.Persistence;
using Data.Enums;
using Data.Results;
using Data.Rules;
using Data.Upgrades;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Presentation;
using R3;
using Services;
using Services.Calculators;
using Services.Locator;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;
using Utils;
using Utils.Metadata;
using Utils.Text.Generator;

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

            PurchaseAndComplete(upgrades, "first");
            Assert.That(unlocks.IsUnlocked(FeatureIds.Office), Is.True);
            PurchaseAndComplete(upgrades, "second");
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

            PurchaseAndComplete(upgrades, "invalid");
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
            var modifiedMedian = (OfficeEntries)wrapper.Apply(source,
                nameof(OfficeEntries.BaseClerkMultiplierMedian), NumericModifierOperation.Override, 2.75d);
            Assert.That(modifiedMedian.BaseClerkMultiplierMedian, Is.EqualTo(2.75d).Within(0.0001d));

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
            unsafeValues.BaseClerkMultiplierMedian = double.NaN;
            unsafeValues.ClerkMultiplierRangeStep = double.PositiveInfinity;
            unsafeValues.MinimumClerkMultiplier = -1d;
            unsafeValues.MaximumHireSignatureMultiplier = double.NaN;
            LogAssert.Expect(LogType.Warning, "Invalid effective office values were normalized to safe ranges.");

            OfficeEntries safe = OfficeCacheCalculator.NormalizeEffective(unsafeValues);
            Assert.That(safe.DocumentsPerSecondPerClerk, Is.Zero);
            Assert.That(safe.QualityCeiling, Is.Zero);
            Assert.That(safe.AcceptanceThreshold, Is.EqualTo(1f));
            Assert.That(safe.RewardMultiplier, Is.Zero);
            Assert.That(safe.BaseClerkMultiplierMedian,
                Is.EqualTo(OfficeCacheCalculator.DefaultBaseClerkMultiplierMedian));
            Assert.That(safe.ClerkMultiplierRangeStep,
                Is.EqualTo(OfficeCacheCalculator.DefaultClerkMultiplierRangeStep));
            Assert.That(safe.MinimumClerkMultiplier, Is.Zero);
            Assert.That(safe.MaximumHireSignatureMultiplier,
                Is.EqualTo(OfficeCacheCalculator.DefaultMaximumHireSignatureMultiplier));
        }

        [Test]
        public void Hiring_RequiresUnlockFundsAndCapacityAndReactsToWalletChanges() {
            OfficeHarness harness = CreateHarness(CreateEntries(capacity: 2));
            Assert.That(harness.Office.CanStartClerkHire(Value.One), Is.False);

            harness.UnlockOffice();
            Assert.That(harness.Office.CanStartClerkHire(Value.One), Is.True);
            Assert.That(harness.Office.TryStartClerkHire(Value.One), Is.True);
            Assert.That(harness.Office.ClerkCount, Is.Zero);
            Assert.That(harness.Office.PendingHireCount, Is.EqualTo(1));

            Assert.That(harness.Wallet.TryWithdrawWallet(new Value(98)), Is.True);
            Assert.That(harness.Office.CanStartClerkHire(Value.One), Is.False);
            harness.Wallet.ReplenishWallet(new Value(10));
            Assert.That(harness.Office.CanStartClerkHire(Value.One), Is.True);
            Assert.That(harness.Office.TryStartClerkHire(Value.One), Is.True);
            Assert.That(harness.Office.CanStartClerkHire(Value.One), Is.False);
            Assert.That(harness.Office.PendingHireCount, Is.EqualTo(2));
        }

        [Test]
        public void Hiring_UsesLogarithmicRangeMinimumAndSnapshottedSignatureMaximum() {
            OfficeEntries entries = CreateEntries(capacity: 2);
            entries.ClerkMultiplierRangeStep = 1d;
            entries.MaximumHireSignatureMultiplier = 3d;
            OfficeHarness harness = CreateHarness(entries, () => 1f);
            harness.UnlockOffice();

            Assert.That(harness.Office.TryStartClerkHire(new Value(10)), Is.True);
            OfficeEntries upgraded = harness.OfficeCalculator.Value;
            upgraded.MaximumHireSignatureMultiplier = 10d;
            harness.OfficeCalculator.Value = upgraded;
            ((ICacheInvalidator)harness.Cache).Invalidate<OfficeEntries>();
            Assert.That(harness.Office.TryClaimPendingClerkHire(out OfficeService.ClerkHireDocumentClaim claim),
                Is.True);
            Assert.That(harness.Office.TryCompletePendingClerkHire(claim,
                Evaluation(SignatureEvaluationStatus.Accepted, 1f, 0.4f)), Is.True);

            Assert.That(harness.Office.Clerks[0].IncomeMultiplier, Is.EqualTo(12d).Within(0.0001d),
                "bid 10 gives median 3, the upper range gives 4, and the snapshotted signature maximum is 3");

            OfficeEntries minimumEntries = CreateEntries();
            minimumEntries.ClerkMultiplierRangeStep = 10d;
            minimumEntries.MinimumClerkMultiplier = 4d;
            OfficeHarness minimumHarness = CreateHarness(minimumEntries, () => 0f);
            minimumHarness.UnlockOffice();
            HireClerk(minimumHarness);
            Assert.That(minimumHarness.Office.Clerks[0].IncomeMultiplier, Is.EqualTo(4d));
        }

        [Test]
        public void Hiring_RejectsNonDebitedBidAndRandomFailureCannotCharge() {
            OfficeHarness insignificantHarness = CreateHarness(CreateEntries());
            insignificantHarness.UnlockOffice();
            insignificantHarness.Wallet.Deserialize(new JObject { ["stored"] = 1d, ["degree"] = 4 });
            Value largeBalance = insignificantHarness.Wallet.CurrentBalance;

            Assert.That(insignificantHarness.Office.TryStartClerkHire(Value.One), Is.False);
            Assert.That(insignificantHarness.Wallet.CurrentBalance, Is.EqualTo(largeBalance));
            Assert.That(insignificantHarness.Office.PendingHireCount, Is.Zero);

            OfficeHarness throwingHarness = CreateHarness(CreateEntries(), () =>
                throw new InvalidOperationException("random failed"));
            throwingHarness.UnlockOffice();
            Value balanceBefore = throwingHarness.Wallet.CurrentBalance;

            Assert.Throws<InvalidOperationException>(() => throwingHarness.Office.TryStartClerkHire(Value.One));
            Assert.That(throwingHarness.Wallet.CurrentBalance, Is.EqualTo(balanceBefore));
            Assert.That(throwingHarness.Office.PendingHireCount, Is.Zero);
        }

        [Test]
        public void Hiring_RandomCallbackAndCompletionNotificationsCannotReenterOfficeMutation() {
            OfficeService randomTarget = null;
            bool nestedRandomStart = true;
            OfficeHarness randomHarness = CreateHarness(CreateEntries(capacity: 2), () => {
                nestedRandomStart = randomTarget.TryStartClerkHire(Value.One);
                return 0.5f;
            });
            randomTarget = randomHarness.Office;
            randomHarness.UnlockOffice();

            Assert.That(randomHarness.Office.TryStartClerkHire(Value.One), Is.True);
            Assert.That(nestedRandomStart, Is.False);
            Assert.That(randomHarness.Office.PendingHireCount, Is.EqualTo(1));
            JToken walletRestore = randomHarness.Office.Serialize();
            bool observeWallet = false;
            bool walletNestedStart = true;
            bool walletNestedClaim = true;
            bool walletRestoreBlocked = false;
            using IDisposable walletSubscription = randomHarness.Wallet.BalanceChanged.Subscribe(balance => {
                if (!observeWallet) return;
                walletNestedStart = randomHarness.Office.TryStartClerkHire(Value.One);
                walletNestedClaim = randomHarness.Office.TryClaimPendingClerkHire(out _);
                walletRestoreBlocked = Assert.Throws<InvalidOperationException>(
                    () => randomHarness.Office.Deserialize(walletRestore)) != null;
                randomHarness.Office.Tick(1f);
            });
            observeWallet = true;

            Assert.That(randomHarness.Office.TryStartClerkHire(Value.One), Is.True);
            Assert.That(walletNestedStart, Is.False);
            Assert.That(walletNestedClaim, Is.False);
            Assert.That(walletRestoreBlocked, Is.True);

            OfficeHarness completionHarness = CreateHarness(CreateEntries(capacity: 2));
            completionHarness.UnlockOffice();
            Assert.That(completionHarness.Office.TryStartClerkHire(Value.One), Is.True);
            Assert.That(completionHarness.Office.TryStartClerkHire(Value.One), Is.True);
            Assert.That(completionHarness.Office.TryClaimPendingClerkHire(
                out OfficeService.ClerkHireDocumentClaim first), Is.True);
            Assert.That(completionHarness.Office.TryClaimPendingClerkHire(
                out OfficeService.ClerkHireDocumentClaim second), Is.True);
            bool observe = true;
            bool nestedStart = true;
            bool nestedClaim = true;
            bool nestedCompletion = true;
            int changes = 0;
            using IDisposable subscription = completionHarness.Office.Changed.Subscribe(unit => {
                if (!observe) return;
                changes++;
                nestedStart = completionHarness.Office.TryStartClerkHire(Value.One);
                nestedClaim = completionHarness.Office.TryClaimPendingClerkHire(out _);
                nestedCompletion = completionHarness.Office.TryCompletePendingClerkHire(second,
                    Evaluation(SignatureEvaluationStatus.Accepted, 0.4f, 0.4f));
            });

            Assert.That(completionHarness.Office.TryCompletePendingClerkHire(first,
                Evaluation(SignatureEvaluationStatus.Accepted, 0.4f, 0.4f)), Is.True);

            Assert.That(changes, Is.EqualTo(1));
            Assert.That(nestedStart, Is.False);
            Assert.That(nestedClaim, Is.False);
            Assert.That(nestedCompletion, Is.False);
            observe = false;
            Assert.That(completionHarness.Office.TryCompletePendingClerkHire(second,
                Evaluation(SignatureEvaluationStatus.Accepted, 0.4f, 0.4f)), Is.True);
        }

        [Test]
        public void HiringProducer_ReleasesReissuesRefundsOnceAndExcludesPlayerModifiers() {
            OfficeHarness harness = CreateHarness(CreateEntries(capacity: 2));
            harness.UnlockOffice();
            Assert.That(harness.Office.TryStartClerkHire(new Value(10)), Is.True);
            var producer = new ClerkHireDocumentProducer();
            producer.InitializeAsync(harness.Scope).GetAwaiter().GetResult();
            Assert.That(producer.TryProduce(out IDocumentSession first), Is.True);
            var baseRules = new SignatureDifficultyRules("base", 0.4f, 1f, 1f, 1f, null);
            var playerModifiers = new SignatureRuleModifiers(2f, -0.2f, 2f, 2f, 2f);
            DocumentEvaluationInputs inputs = first.EvaluationPolicy.Resolve(baseRules, playerModifiers);
            Assert.That(inputs.Difficulty, Is.SameAs(baseRules));
            Assert.That(inputs.Modifiers.MinimumSimilarityOffset, Is.Zero);
            first.Dispose();

            Assert.That(producer.TryProduce(out IDocumentSession reissued), Is.True);
            Assert.That(reissued.TryProcess(Evaluation(SignatureEvaluationStatus.Rejected, 0.2f, 0.4f)), Is.True);
            Assert.That(reissued.TryProcess(Evaluation(SignatureEvaluationStatus.Rejected, 0.2f, 0.4f)), Is.False);
            reissued.Dispose();
            Assert.That(harness.Office.ClerkCount, Is.Zero);
            Assert.That(harness.Office.PendingHireCount, Is.Zero);
            Assert.That(harness.Wallet.CurrentBalance, Is.EqualTo(new Value(99)));

            Assert.That(harness.Office.TryStartClerkHire(new Value(5)), Is.True);
            Assert.That(harness.Office.TryClaimPendingClerkHire(out OfficeService.ClerkHireDocumentClaim invalid),
                Is.True);
            Assert.That(harness.Office.TryCompletePendingClerkHire(invalid,
                Evaluation(SignatureEvaluationStatus.InvalidAttempt, 0f, 0f)), Is.True);
            Assert.That(harness.Wallet.CurrentBalance, Is.EqualTo(new Value(99)));
        }

        [Test]
        public void Hiring_AcceptedMalformedEvaluationUsesNeutralSignatureMultiplier() {
            OfficeEntries entries = CreateEntries();
            entries.MaximumHireSignatureMultiplier = 5d;
            OfficeHarness harness = CreateHarness(entries);
            harness.UnlockOffice();
            Assert.That(harness.Office.TryStartClerkHire(Value.One), Is.True);
            Assert.That(harness.Office.TryClaimPendingClerkHire(out OfficeService.ClerkHireDocumentClaim claim),
                Is.True);

            Assert.That(harness.Office.TryCompletePendingClerkHire(claim,
                Evaluation(SignatureEvaluationStatus.Accepted, float.NaN, 0.4f)), Is.True);

            Assert.That(harness.Office.Clerks[0].IncomeMultiplier, Is.EqualTo(2d));
        }

        [Test]
        public void HiringPersistence_InvalidatesClaimsMigratesLegacyAndPreservesRequestCounter() {
            OfficeHarness harness = CreateHarness(CreateEntries(capacity: 2));
            harness.UnlockOffice();
            Assert.That(harness.Office.TryStartClerkHire(Value.One), Is.True);
            var producer = new ClerkHireDocumentProducer();
            producer.InitializeAsync(harness.Scope).GetAwaiter().GetResult();
            Assert.That(producer.TryProduce(out IDocumentSession stale), Is.True);
            JToken pendingSave = harness.Office.Serialize();

            harness.Office.Deserialize(pendingSave);

            Assert.That(stale.TryProcess(Evaluation(SignatureEvaluationStatus.Accepted, 0.4f, 0.4f)), Is.False);
            stale.Dispose();
            Assert.That(producer.TryProduce(out IDocumentSession restored), Is.True);
            Assert.That(restored.TryProcess(Evaluation(SignatureEvaluationStatus.Accepted, 0.4f, 0.4f)), Is.True);
            restored.Dispose();
            JToken completedSave = harness.Office.Serialize();
            Assert.That(completedSave["nextHireRequestId"]?.Value<long>(), Is.EqualTo(2L));
            Assert.That((completedSave["pendingHires"] as JArray)?.Count, Is.Zero);

            harness.Office.Deserialize(completedSave);
            Assert.That(harness.Office.TryStartClerkHire(Value.One), Is.True);
            JObject secondPending = (JObject)((JArray)harness.Office.Serialize()["pendingHires"])[0];
            Assert.That(secondPending["requestId"]?.Value<long>(), Is.EqualTo(2L));

            harness.Office.Deserialize(new JObject {
                ["nextClerkId"] = 2,
                ["clerks"] = new JArray(new JObject { ["id"] = 1, ["progress"] = 0.5f })
            });
            Assert.That(harness.Office.Clerks[0].IncomeMultiplier, Is.EqualTo(1d));

            Assert.Throws<JsonSerializationException>(() => harness.Office.Deserialize(new JObject {
                ["nextClerkId"] = 1,
                ["clerks"] = new JArray(),
                ["pendingHires"] = new JArray()
            }));
        }

        [Test]
        public void HiringPersistence_RoundTripsZeroAndExtremeMultipliersAndChecksIdHeadroom() {
            OfficeHarness harness = CreateHarness(CreateEntries());
            harness.UnlockOffice();
            var extreme = new JObject {
                ["nextClerkId"] = 2,
                ["nextHireRequestId"] = 2L,
                ["clerks"] = new JArray(new JObject {
                    ["id"] = 1,
                    ["progress"] = 0f,
                    ["incomeMultiplier"] = 0d
                }),
                ["pendingHires"] = new JArray(new JObject {
                    ["requestId"] = 1L,
                    ["paidStored"] = 1d,
                    ["paidDegree"] = 0,
                    ["rolledBaseMultiplier"] = double.MaxValue,
                    ["maximumSignatureMultiplier"] = double.MaxValue
                })
            };

            harness.Office.Deserialize(extreme);
            JToken roundTrip = harness.Office.Serialize();
            harness.Office.Deserialize(roundTrip);
            Assert.That(harness.Office.Clerks[0].IncomeMultiplier, Is.Zero);
            Assert.That(harness.Office.PendingHireCount, Is.EqualTo(1));

            harness.Office.Deserialize(new JObject {
                ["nextClerkId"] = int.MaxValue,
                ["nextHireRequestId"] = 1L,
                ["clerks"] = new JArray(),
                ["pendingHires"] = new JArray()
            });
            Assert.That(harness.Office.CanStartClerkHire(Value.One), Is.False);

            harness.Office.Deserialize(new JObject {
                ["nextClerkId"] = 1,
                ["nextHireRequestId"] = long.MaxValue,
                ["clerks"] = new JArray(),
                ["pendingHires"] = new JArray()
            });
            Assert.That(harness.Office.CanStartClerkHire(Value.One), Is.False);
        }

        [Test]
        public void HiringProducer_HasPriorityAndDoesNotConsumeNormalDocument() {
            OfficeHarness harness = CreateHarness(CreateEntries(capacity: 2));
            harness.UnlockOffice();
            Assert.That(harness.Upgrades.TryUpgrade("document_upgrade"), Is.True);
            Assert.That(harness.Office.TryStartClerkHire(Value.One), Is.True);
            SetDocumentCount(harness.Documents, 1);
            var normal = new NormalDocumentProducer();
            var upgrade = new UpgradeDocumentProducer();
            var clerk = new ClerkHireDocumentProducer();
            normal.InitializeAsync(harness.Scope).GetAwaiter().GetResult();
            upgrade.InitializeAsync(harness.Scope).GetAwaiter().GetResult();
            clerk.InitializeAsync(harness.Scope).GetAwaiter().GetResult();
            var viewModel = new DispenseViewModel(
                new IDocumentProducer[] { normal, upgrade, clerk },
                new StaticCache<DocumentEntries>(default),
                new StableRandom(123));

            Assert.That(viewModel.TryCreateContext(out IDocumentContext context), Is.True);
            IDocumentSession selected = context.TakeSession();
            context.Dispose();
            Assert.That(selected.TryProcess(Evaluation(SignatureEvaluationStatus.Accepted, 0.4f, 0.4f)), Is.True);
            selected.Dispose();

            Assert.That(harness.Office.ClerkCount, Is.EqualTo(1));
            Assert.That(harness.Upgrades.GetUpgrade("document_upgrade").CurrentState,
                Is.EqualTo(UpgradeNodeState.State.Pending));
            Assert.That(harness.Documents.Serialize()["documentQuantity"]?.Value<int>(), Is.EqualTo(1));
        }

        [Test]
        public void PendingHire_SurvivesCapacityDecreaseAndExtremeRewardSaturates() {
            OfficeHarness capacityHarness = CreateHarness(CreateEntries(capacity: 2));
            capacityHarness.UnlockOffice();
            Assert.That(capacityHarness.Office.TryStartClerkHire(Value.One), Is.True);
            Assert.That(capacityHarness.Office.TryStartClerkHire(Value.One), Is.True);
            OfficeEntries reduced = capacityHarness.OfficeCalculator.Value;
            reduced.ClerkCapacity = 0;
            capacityHarness.OfficeCalculator.Value = reduced;
            ((ICacheInvalidator)capacityHarness.Cache).Invalidate<OfficeEntries>();
            Assert.That(capacityHarness.Office.TryClaimPendingClerkHire(
                out OfficeService.ClerkHireDocumentClaim first), Is.True);
            Assert.That(capacityHarness.Office.TryClaimPendingClerkHire(
                out OfficeService.ClerkHireDocumentClaim second), Is.True);
            Assert.That(capacityHarness.Office.TryCompletePendingClerkHire(first,
                Evaluation(SignatureEvaluationStatus.Accepted, 0.4f, 0.4f)), Is.True);
            Assert.That(capacityHarness.Office.TryCompletePendingClerkHire(second,
                Evaluation(SignatureEvaluationStatus.Accepted, 0.4f, 0.4f)), Is.True);
            Assert.That(capacityHarness.Office.ClerkCount, Is.EqualTo(2));

            Value extremeIncome = new(1d, new BaseValue(int.MaxValue - 1));
            OfficeHarness rewardHarness = CreateHarness(CreateEntries(), () => 1f, income: extremeIncome);
            rewardHarness.UnlockOffice();
            rewardHarness.Office.Deserialize(new JObject {
                ["nextClerkId"] = 2,
                ["nextHireRequestId"] = 1L,
                ["clerks"] = new JArray(new JObject {
                    ["id"] = 1,
                    ["progress"] = 0f,
                    ["incomeMultiplier"] = double.MaxValue
                }),
                ["pendingHires"] = new JArray()
            });
            SetDocumentCount(rewardHarness.Documents, 1);
            OfficeDocumentResult result = default;
            using IDisposable subscription = rewardHarness.Office.DocumentProcessed.Subscribe(value => result = value);

            rewardHarness.Office.Tick(1f);

            Assert.That(result.RequestedReward, Is.EqualTo(Value.Infinity));
        }

        [Test]
        public void Tick_UsesRoundRobinOrderAndEmitsOneChangedNotification() {
            OfficeHarness harness = CreateHarness(CreateEntries(capacity: 2, speed: 2f), () => 1f);
            harness.UnlockOffice();
            HireClerk(harness);
            HireClerk(harness);
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
            HireClerk(harness);
            HireClerk(harness);
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
            OfficeHarness harness = CreateHarness(CreateEntries(capacity: 3), () => 1f);
            harness.UnlockOffice();
            HireClerk(harness);
            Assert.That(harness.Office.TryStartClerkHire(Value.One), Is.True);
            SetDocumentCount(harness.Documents, 2);
            JToken restore = harness.Office.Serialize();
            int results = 0;
            int nestedTickAttempts = 0;
            bool nestedStart = true;
            bool nestedClaim = true;
            using IDisposable resultSubscription = harness.Office.DocumentProcessed.Subscribe(documentResult => {
                results++;
                nestedStart = harness.Office.TryStartClerkHire(Value.One);
                nestedClaim = harness.Office.TryClaimPendingClerkHire(out _);
                Assert.Throws<InvalidOperationException>(() => harness.Office.Deserialize(restore));
            });
            using IDisposable changedSubscription = harness.Office.Changed.Subscribe(_ => {
                nestedTickAttempts++;
                harness.Office.Tick(1f);
            });

            harness.Office.Tick(1f);

            Assert.That(results, Is.EqualTo(1));
            Assert.That(nestedTickAttempts, Is.EqualTo(1));
            Assert.That(nestedStart, Is.False);
            Assert.That(nestedClaim, Is.False);
            Assert.That(harness.Office.Clerks[0].Progress, Is.Zero);
        }

        [Test]
        public void Tick_AcceptsAndRejectsUsingOfficeRewardRules() {
            var rolls = new Queue<float>(new[] { 0.5f, 1f, 0f });
            OfficeHarness harness = CreateHarness(CreateEntries(speed: 2f), () => rolls.Dequeue());
            harness.UnlockOffice();
            HireClerk(harness);
            SetDocumentCount(harness.Documents, 2);
            var results = new List<OfficeDocumentResult>();
            using IDisposable subscription = harness.Office.DocumentProcessed.Subscribe(results.Add);
            double before = harness.Wallet.CurrentBalance.ToDouble();

            harness.Office.Tick(1f);

            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results[0].Accepted, Is.True);
            Assert.That(results[0].RequestedReward.ToDouble(), Is.EqualTo(1d).Within(0.0001d));
            Assert.That(results[0].CreditedReward.ToDouble(), Is.EqualTo(1d).Within(0.0001d));
            Assert.That(results[1].Accepted, Is.False);
            Assert.That(harness.Wallet.CurrentBalance.ToDouble(), Is.EqualTo(before + 1d).Within(0.0001d));
            AssertStatistic(harness.Statistics, GameStatisticIds.OfficeAcceptedDocuments, 1d);
            AssertStatistic(harness.Statistics, GameStatisticIds.OfficeRejectedDocuments, 1d);
        }

        [Test]
        public void Tick_StarvationStoresReadyStateAndInvalidDeltaDoesNotMutate() {
            OfficeHarness harness = CreateHarness(CreateEntries(speed: 1f));
            harness.UnlockOffice();
            HireClerk(harness);
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
            HireClerk(harness);
            HireClerk(harness);
            int notifications = 0;
            using IDisposable subscription = harness.Office.Changed.Subscribe(_ => notifications++);

            OfficeEntries reduced = harness.OfficeCalculator.Value;
            reduced.ClerkCapacity = 1;
            harness.OfficeCalculator.Value = reduced;
            ((ICacheInvalidator)harness.Cache).Invalidate<OfficeEntries>();

            Assert.That(harness.Office.ClerkCount, Is.EqualTo(2));
            Assert.That(harness.Office.ClerkCapacity, Is.EqualTo(1));
            Assert.That(harness.Office.CanStartClerkHire(Value.One), Is.False);
            Assert.That(notifications, Is.EqualTo(1));
        }

        [Test]
        public void Persistence_RoundTripsAndMalformedRestoreIsAtomic() {
            OfficeHarness harness = CreateHarness(CreateEntries());
            harness.UnlockOffice();
            HireClerk(harness);
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
            Assert.That(harness.Office.Clerks[0].IncomeMultiplier, Is.EqualTo(2d));
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
            HireClerk(harness);
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
            HireClerk(harness);
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
            Observable<float> updates = null, Value? income = null) {
            UpgradeNodeDefinition officeUnlock = CreateUpgrade("office_unlock", FeatureIds.Office);
            UpgradeNodeDefinition documentUpgrade = CreateUpgrade("document_upgrade");
            var provider = new FakeAssetProvider(new[] { officeUnlock, documentUpgrade });
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
                    new IncomeEntries(1f, 0.4f, income ?? Value.One)))
                .Register<ICacheCalculator<SignatureEntries>>(new StaticCalculator<SignatureEntries>(default))
                .Register<ICacheCalculator<GenerationEntries>>(new StaticCalculator<GenerationEntries>(default))
                .Register<ICacheCalculator<OfficeEntries>>(officeCalculator)
                .Register<ICacheCalculator<DocumentEntries>>(new StaticCalculator<DocumentEntries>(default))
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
                BaseClerkMultiplierMedian = 2d,
                ClerkMultiplierRangeStep = 0d,
                MinimumClerkMultiplier = 1d,
                MaximumHireSignatureMultiplier = 2d
            };
        }

        private static void HireClerk(OfficeHarness harness, Value? bid = null, float similarity = 0.4f,
            float minimumSimilarity = 0.4f) {
            Assert.That(harness.Office.TryStartClerkHire(bid ?? Value.One), Is.True);
            Assert.That(harness.Office.TryClaimPendingClerkHire(out OfficeService.ClerkHireDocumentClaim claim),
                Is.True);
            Assert.That(harness.Office.TryCompletePendingClerkHire(claim, new SignatureEvaluationResult(
                SignatureEvaluationStatus.Accepted,
                SignatureFailureReason.None,
                similarity,
                minimumSimilarity,
                null)), Is.True);
        }

        private static SignatureEvaluationResult Evaluation(
            SignatureEvaluationStatus status,
            float similarity,
            float minimumSimilarity) {
            return new SignatureEvaluationResult(
                status,
                status == SignatureEvaluationStatus.Accepted
                    ? SignatureFailureReason.None
                    : SignatureFailureReason.BelowSimilarityThreshold,
                similarity,
                minimumSimilarity,
                null);
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

        private static void PurchaseAndComplete(UpgradeService upgrades, string upgradeId) {
            Assert.That(upgrades.TryUpgrade(upgradeId), Is.True);
            Assert.That(upgrades.TryClaimPendingUpgrade(out UpgradeService.UpgradeDocumentClaim claim), Is.True);
            Assert.That(upgrades.TryCompletePendingUpgrade(claim, new SignatureEvaluationResult(
                SignatureEvaluationStatus.Accepted,
                SignatureFailureReason.None,
                1f,
                0.4f,
                null)), Is.True);
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
                PurchaseAndComplete(Upgrades, "office_unlock");
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

        private sealed class StaticCache<T> : IReadOnlyCacheData<T> {
            public T Value { get; }
            public StaticCache(T value) => Value = value;
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
