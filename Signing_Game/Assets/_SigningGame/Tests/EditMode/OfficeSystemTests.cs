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
            unsafeValues.OfficeSignatureCriticalChance = float.PositiveInfinity;
            unsafeValues.OfficeSignatureCriticalMultiplier = double.NaN;
            unsafeValues.BaseClerkMultiplierMedian = double.NaN;
            unsafeValues.ClerkMultiplierRangeStep = double.PositiveInfinity;
            unsafeValues.MinimumClerkMultiplier = -1d;
            unsafeValues.MaximumHireSignatureMultiplier = double.NaN;
            unsafeValues.SalaryReviewCostRatio = double.NaN;
            LogAssert.Expect(LogType.Warning, "Invalid effective office values were normalized to safe ranges.");

            OfficeEntries safe = OfficeCacheCalculator.NormalizeEffective(unsafeValues);
            Assert.That(safe.DocumentsPerSecondPerClerk, Is.Zero);
            Assert.That(safe.QualityCeiling, Is.Zero);
            Assert.That(safe.AcceptanceThreshold, Is.EqualTo(1f));
            Assert.That(safe.RewardMultiplier, Is.Zero);
            Assert.That(safe.OfficeSignatureCriticalChance, Is.Zero);
            Assert.That(safe.OfficeSignatureCriticalMultiplier,
                Is.EqualTo(OfficeCacheCalculator.DefaultOfficeSignatureCriticalMultiplier));
            Assert.That(safe.BaseClerkMultiplierMedian,
                Is.EqualTo(OfficeCacheCalculator.DefaultBaseClerkMultiplierMedian));
            Assert.That(safe.ClerkMultiplierRangeStep,
                Is.EqualTo(OfficeCacheCalculator.DefaultClerkMultiplierRangeStep));
            Assert.That(safe.MinimumClerkMultiplier, Is.Zero);
            Assert.That(safe.MaximumHireSignatureMultiplier,
                Is.EqualTo(OfficeCacheCalculator.DefaultMaximumHireSignatureMultiplier));
            Assert.That(safe.SalaryReviewCostRatio,
                Is.EqualTo(OfficeCacheCalculator.DefaultSalaryReviewCostRatio));
        }

        [Test]
        public void SignatureCriticalRandom_RestoresManualAndOfficeStreams() {
            var source = new SignatureCriticalRandomService(123UL);
            source.RollManual(0.5f);
            source.RollOffice(0.5f);
            JToken saved = source.Serialize();
            var restored = new SignatureCriticalRandomService(999UL);

            restored.Deserialize(saved);

            Assert.That(restored.RollManual(0.5f), Is.EqualTo(source.RollManual(0.5f)));
            Assert.That(restored.RollOffice(0.5f), Is.EqualTo(source.RollOffice(0.5f)));
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
            var effectiveRules = baseRules with { MinimumSimilarity = 0.2f, CorridorWidthMultiplier = 2f };
            DocumentEvaluationInputs inputs = first.EvaluationPolicy.Resolve(
                new SignatureDifficultyContext(baseRules, effectiveRules));
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
        public void Hiring_PersistsRandomProfileAndDecomposesEfficiencyWithoutChangingFinalMultiplier() {
            var rolls = new Queue<float>(new[] { 0.5f, 0f, 1f });
            OfficeHarness harness = CreateHarness(CreateEntries(), () => rolls.Dequeue());
            harness.UnlockOffice();

            HireClerk(harness, new Value(10), 1f, 0.4f);

            OfficeClerkState clerk = harness.Office.Clerks[0];
            Assert.That(clerk.Name, Is.EqualTo("Alex"));
            Assert.That(clerk.Age, Is.EqualTo(65));
            Assert.That(clerk.OriginalHirePrice, Is.EqualTo(new Value(10)));
            Assert.That(clerk.BaseEfficiency, Is.EqualTo(3d));
            Assert.That(clerk.BonusEfficiency, Is.EqualTo(1d));
            Assert.That(clerk.IncomeMultiplier, Is.EqualTo(6d));

            JObject serialized = (JObject)harness.Office.Serialize();
            JObject savedClerk = (JObject)((JArray)serialized["clerks"])[0];
            Assert.That(savedClerk["incomeMultiplier"], Is.Null);
            harness.Office.Deserialize(serialized);
            Assert.That(harness.Office.Clerks[0].Name, Is.EqualTo("Alex"));
            Assert.That(harness.Office.Clerks[0].IncomeMultiplier, Is.EqualTo(6d));
        }

        [Test]
        public void Hiring_AllProfileRandomFailuresOccurBeforeDebit() {
            for (int failingDraw = 1; failingDraw <= 3; failingDraw++) {
                int calls = 0;
                OfficeHarness harness = CreateHarness(CreateEntries(), () => {
                    calls++;
                    if (calls == failingDraw) throw new InvalidOperationException("random failed");
                    return 0.5f;
                });
                harness.UnlockOffice();
                Value before = harness.Wallet.CurrentBalance;

                Assert.Throws<InvalidOperationException>(() => harness.Office.TryStartClerkHire(Value.One));
                Assert.That(harness.Wallet.CurrentBalance, Is.EqualTo(before));
                Assert.That(harness.Office.PendingHireCount, Is.Zero);
            }
        }

        [Test]
        public void SalaryReview_DebitsOriginalBidAndAcceptedSignatureReplacesBonus() {
            OfficeHarness harness = CreateHarness(CreateEntries());
            harness.UnlockOffice();
            HireClerk(harness, new Value(10), 0.4f, 0.4f);
            OfficeClerkState clerk = harness.Office.Clerks[0];
            Assert.That(clerk.BonusEfficiency, Is.Zero);
            Value before = harness.Wallet.CurrentBalance;

            Value reviewCost = harness.Office.GetSalaryReviewCost(clerk.Id);
            Assert.That(reviewCost.ToDouble(), Is.EqualTo(5d).Within(0.000001d));
            Assert.That(harness.Office.TryStartSalaryReview(clerk.Id), Is.True);
            Assert.That(harness.Wallet.CurrentBalance, Is.EqualTo((before - reviewCost).Value));
            Assert.That(harness.Office.TryClaimPendingSalaryReview(
                out OfficeService.SalaryReviewDocumentClaim claim), Is.True);
            Assert.That(harness.Office.TryCompletePendingSalaryReview(
                claim,
                Evaluation(SignatureEvaluationStatus.Accepted, 1f, 0.4f)), Is.True);

            Assert.That(clerk.BonusEfficiency, Is.EqualTo(1d));
            Assert.That(clerk.IncomeMultiplier, Is.EqualTo(6d));
            Assert.That(harness.Office.PendingSalaryReviewCount, Is.Zero);
        }

        [Test]
        public void SalaryReview_RejectionAndMalformedAcceptanceNeverRefundOrChangeBonus() {
            OfficeHarness rejected = CreateHarness(CreateEntries());
            rejected.UnlockOffice();
            HireClerk(rejected, Value.One, 1f, 0.4f);
            OfficeClerkState rejectedClerk = rejected.Office.Clerks[0];
            Assert.That(rejected.Office.TryStartSalaryReview(rejectedClerk.Id), Is.True);
            Value afterDebit = rejected.Wallet.CurrentBalance;
            Assert.That(rejected.Office.TryClaimPendingSalaryReview(
                out OfficeService.SalaryReviewDocumentClaim rejectedClaim), Is.True);
            Assert.That(rejected.Office.TryCompletePendingSalaryReview(
                rejectedClaim,
                Evaluation(SignatureEvaluationStatus.Rejected, 0.2f, 0.4f)), Is.True);
            Assert.That(rejectedClerk.BonusEfficiency, Is.EqualTo(1d));
            Assert.That(rejected.Wallet.CurrentBalance, Is.EqualTo(afterDebit));

            Assert.That(rejected.Office.TryStartSalaryReview(rejectedClerk.Id), Is.True);
            Value afterSecondDebit = rejected.Wallet.CurrentBalance;
            Assert.That(rejected.Office.TryClaimPendingSalaryReview(
                out OfficeService.SalaryReviewDocumentClaim malformedClaim), Is.True);
            Assert.That(rejected.Office.TryCompletePendingSalaryReview(
                malformedClaim,
                Evaluation(SignatureEvaluationStatus.Accepted, float.NaN, 0.4f)), Is.True);
            Assert.That(rejectedClerk.BonusEfficiency, Is.EqualTo(1d));
            Assert.That(rejected.Wallet.CurrentBalance, Is.EqualTo(afterSecondDebit));
        }

        [Test]
        public void SalaryReview_FreeAndInsignificantCostsHaveExplicitEligibility() {
            OfficeEntries freeEntries = CreateEntries();
            freeEntries.SalaryReviewCostRatio = 0d;
            OfficeHarness free = CreateHarness(freeEntries);
            free.UnlockOffice();
            HireClerk(free);
            Value freeBalance = free.Wallet.CurrentBalance;
            Assert.That(free.Office.TryStartSalaryReview(free.Office.Clerks[0].Id), Is.True);
            Assert.That(free.Wallet.CurrentBalance, Is.EqualTo(freeBalance));

            OfficeHarness insignificant = CreateHarness(CreateEntries());
            insignificant.UnlockOffice();
            HireClerk(insignificant);
            insignificant.Wallet.Deserialize(new JObject { ["stored"] = 1d, ["degree"] = 4 });
            Assert.That(insignificant.Office.CanStartSalaryReview(insignificant.Office.Clerks[0].Id), Is.False);
            Assert.That(insignificant.Office.TryStartSalaryReview(insignificant.Office.Clerks[0].Id), Is.False);
        }

        [Test]
        public void SalaryReview_TargetedDismissalInvalidatesOnlyThatClerksClaim() {
            OfficeHarness harness = CreateHarness(CreateEntries(capacity: 2));
            harness.UnlockOffice();
            HireClerk(harness);
            HireClerk(harness);
            int firstId = harness.Office.Clerks[0].Id;
            int secondId = harness.Office.Clerks[1].Id;
            Assert.That(harness.Office.TryStartSalaryReview(firstId), Is.True);
            Assert.That(harness.Office.TryStartSalaryReview(secondId), Is.True);
            Assert.That(harness.Office.TryClaimPendingSalaryReview(
                out OfficeService.SalaryReviewDocumentClaim firstClaim), Is.True);
            Assert.That(harness.Office.TryClaimPendingSalaryReview(
                out OfficeService.SalaryReviewDocumentClaim secondClaim), Is.True);

            Assert.That(harness.Office.TryDismissClerk(firstId), Is.True);

            Assert.That(harness.Office.TryCompletePendingSalaryReview(
                firstClaim,
                Evaluation(SignatureEvaluationStatus.Accepted, 1f, 0.4f)), Is.False);
            Assert.That(harness.Office.TryCompletePendingSalaryReview(
                secondClaim,
                Evaluation(SignatureEvaluationStatus.Accepted, 1f, 0.4f)), Is.True);
            Assert.That(harness.Office.ClerkCount, Is.EqualTo(1));
            Assert.That(harness.Office.Clerks[0].Id, Is.EqualTo(secondId));
            AssertStatistic(harness.Statistics, GameStatisticIds.OfficeClerkCount, 1d);
        }

        [Test]
        public void SalaryReview_RestoreInvalidatesClaimsButMalformedRestorePreservesThem() {
            OfficeHarness harness = CreateHarness(CreateEntries());
            harness.UnlockOffice();
            HireClerk(harness);
            int clerkId = harness.Office.Clerks[0].Id;
            Assert.That(harness.Office.TryStartSalaryReview(clerkId), Is.True);
            Assert.That(harness.Office.TryClaimPendingSalaryReview(
                out OfficeService.SalaryReviewDocumentClaim originalClaim), Is.True);
            JObject valid = (JObject)harness.Office.Serialize();
            JObject malformed = (JObject)valid.DeepClone();
            ((JObject)((JArray)malformed["clerks"])[0]).Remove("age");

            Assert.Throws<JsonSerializationException>(() => harness.Office.Deserialize(malformed));
            Assert.That(harness.Office.TryReleasePendingSalaryReview(originalClaim), Is.True);
            Assert.That(harness.Office.TryClaimPendingSalaryReview(
                out OfficeService.SalaryReviewDocumentClaim staleAfterRestore), Is.True);

            harness.Office.Deserialize(valid);

            Assert.That(harness.Office.TryCompletePendingSalaryReview(
                staleAfterRestore,
                Evaluation(SignatureEvaluationStatus.Accepted, 1f, 0.4f)), Is.False);
            Assert.That(harness.Office.TryClaimPendingSalaryReview(out _), Is.True);
        }

        [Test]
        public void SalaryReviewPersistence_RejectsUnknownAndDuplicateClerkReferencesAtomically() {
            OfficeHarness harness = CreateHarness(CreateEntries());
            harness.UnlockOffice();
            HireClerk(harness);
            Assert.That(harness.Office.TryStartSalaryReview(harness.Office.Clerks[0].Id), Is.True);
            JObject before = (JObject)harness.Office.Serialize();

            JObject unknown = (JObject)before.DeepClone();
            ((JObject)((JArray)unknown["pendingSalaryReviews"])[0])["clerkId"] = 999;
            Assert.Throws<JsonSerializationException>(() => harness.Office.Deserialize(unknown));
            Assert.That(JToken.DeepEquals(harness.Office.Serialize(), before), Is.True);

            JObject duplicate = (JObject)before.DeepClone();
            JObject duplicateRecord = (JObject)((JArray)duplicate["pendingSalaryReviews"])[0].DeepClone();
            duplicateRecord["requestId"] = 2L;
            ((JArray)duplicate["pendingSalaryReviews"]).Add(duplicateRecord);
            duplicate["nextSalaryReviewRequestId"] = 3L;
            Assert.Throws<JsonSerializationException>(() => harness.Office.Deserialize(duplicate));
            Assert.That(JToken.DeepEquals(harness.Office.Serialize(), before), Is.True);
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

            Assert.That(viewModel.TryCreateContext(viewModel.Current, out IDocumentContext context), Is.True);
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
        public void NormalOffer_DoesNotReserveUntilExactClaimAndLosingLastDocumentRejectsClaim() {
            OfficeHarness harness = CreateHarness(CreateEntries());
            SetDocumentCount(harness.Documents, 1);
            var producer = new NormalDocumentProducer();
            producer.InitializeAsync(harness.Scope).GetAwaiter().GetResult();

            Assert.That(producer.TryPeekOffer(out DocumentOffer offer), Is.True);
            Assert.That(offer.IsAvailable, Is.True);
            Assert.That(harness.Documents.DocumentQuantity, Is.EqualTo(1));

            Assert.That(harness.Documents.TryObtainDocument(), Is.True);
            Assert.That(producer.TryProduce(offer.Key, out _), Is.False);
            Assert.That(producer.TryPeekOffer(out DocumentOffer unavailable), Is.True);
            Assert.That(unavailable.IsAvailable, Is.False);

            SetDocumentCount(harness.Documents, 1);
            Assert.That(producer.TryProduce(offer.Key, out IDocumentSession session), Is.True);
            Assert.That(harness.Documents.DocumentQuantity, Is.Zero);
            Assert.That(producer.TryProduce(offer.Key, out _), Is.False);
            session.Dispose();
            Assert.That(harness.Documents.DocumentQuantity, Is.EqualTo(1));
        }

        [Test]
        public void NormalDocumentReward_AppliesManualCriticalMultiplier() {
            OfficeHarness harness = CreateHarness(
                CreateEntries(),
                incomeEntries: new IncomeEntries(1f, 0.4f, Value.One, 1f, 3d));
            SetDocumentCount(harness.Documents, 1);
            var producer = new NormalDocumentProducer();
            producer.InitializeAsync(harness.Scope).GetAwaiter().GetResult();
            Assert.That(producer.TryPeekOffer(out DocumentOffer offer), Is.True);
            Assert.That(producer.TryProduce(offer.Key,
                out IDocumentSession session), Is.True);
            double before = harness.Wallet.CurrentBalance.ToDouble();

            Assert.That(session.TryProcess(Evaluation(SignatureEvaluationStatus.Accepted, 1f, 0.4f)), Is.True);

            Assert.That(harness.Wallet.CurrentBalance.ToDouble(), Is.EqualTo(before + 3d).Within(0.0001d));
            session.Dispose();
        }

        [Test]
        public void NormalDocumentReward_MultiPayChancePaysSeparateRewards() {
            OfficeHarness harness = CreateHarness(
                CreateEntries(),
                incomeEntries: new IncomeEntries(1f, 0.4f, Value.One, 0f, 1d, 1f));
            SetDocumentCount(harness.Documents, 1);
            var producer = new NormalDocumentProducer();
            producer.InitializeAsync(harness.Scope).GetAwaiter().GetResult();
            Assert.That(producer.TryPeekOffer(out DocumentOffer offer), Is.True);
            Assert.That(producer.TryProduce(offer.Key,
                out IDocumentSession session), Is.True);
            double before = harness.Wallet.CurrentBalance.ToDouble();

            Assert.That(session.TryProcess(Evaluation(SignatureEvaluationStatus.Accepted, 1f, 0.4f)), Is.True);

            // A whole multi-pay chance of 1.0 grants a second full payment with its own crit roll.
            Assert.That(harness.Wallet.CurrentBalance.ToDouble(), Is.EqualTo(before + 2d).Within(0.0001d));
            session.Dispose();
        }

        [Test]
        public void DocumentQualityIncomeMultiplier_AppliesToManualAndOfficeDocumentIncome() {
            var documentEntries = new DocumentEntries {
                SelectedDocumentQualityLevel = 2,
                DocumentQualityIncomeMultiplier = 0.2f
            };
            OfficeHarness harness = CreateHarness(CreateEntries(), () => 1f, documentEntries: documentEntries);
            SetDocumentCount(harness.Documents, 1);
            var producer = new NormalDocumentProducer();
            producer.InitializeAsync(harness.Scope).GetAwaiter().GetResult();
            Assert.That(producer.TryProduce(out IDocumentSession session), Is.True);
            double beforeManual = harness.Wallet.CurrentBalance.ToDouble();

            Assert.That(session.TryProcess(Evaluation(SignatureEvaluationStatus.Accepted, 1f, 0.4f)), Is.True);
            Assert.That(harness.Wallet.CurrentBalance.ToDouble(), Is.EqualTo(beforeManual + 3.6d).Within(0.0001d));
            session.Dispose();

            harness.UnlockOffice();
            HireClerk(harness);
            SetDocumentCount(harness.Documents, 1);
            OfficeDocumentResult result = default;
            using IDisposable subscription = harness.Office.DocumentProcessed.Subscribe(value => result = value);
            double beforeOffice = harness.Wallet.CurrentBalance.ToDouble();

            harness.Office.Tick(1f);

            Assert.That(result.RequestedReward.ToDouble(), Is.EqualTo(1.8d).Within(0.0001d));
            Assert.That(harness.Wallet.CurrentBalance.ToDouble(), Is.EqualTo(beforeOffice + 1.8d).Within(0.0001d));
        }

        [Test]
        public void DocumentQualityIncomeMultiplier_IsNotCappedAtOne() {
            double result = DocumentQualityRewardMultiplier.Resolve(new DocumentEntries {
                SelectedDocumentQualityLevel = 2,
                DocumentQualityIncomeMultiplier = 2f
            });

            Assert.That(result, Is.EqualTo(9d));
        }

        [Test]
        public void NormalDocumentReward_UsesStampStateForRequiredAndOptionalDocuments() {
            OfficeHarness harness = CreateHarness(
                CreateEntries(),
                incomeEntries: new IncomeEntries(1f, 0.4f, Value.One));
            harness.UnlockStamp();
            SetDocumentCount(harness.Documents, 1);
            var producer = new NormalDocumentProducer();
            producer.InitializeAsync(harness.Scope).GetAwaiter().GetResult();

            Assert.That(producer.TryPeekOffer(out DocumentOffer firstOffer), Is.True);
            Assert.That(firstOffer.RequiresStamp, Is.False);
            Assert.That(producer.TryProduce(firstOffer.Key, out IDocumentSession discarded), Is.True);
            discarded.Dispose();

            Assert.That(producer.TryPeekOffer(out DocumentOffer requiredOffer), Is.True);
            Assert.That(requiredOffer.RequiresStamp, Is.True);
            Assert.That(producer.TryProduce(requiredOffer.Key, out IDocumentSession required), Is.True);
            double beforeRequired = harness.Wallet.CurrentBalance.ToDouble();
            Assert.That(required.TryProcess(Evaluation(SignatureEvaluationStatus.Accepted, 1f, 0.4f), true),
                Is.True);
            Assert.That(harness.Wallet.CurrentBalance.ToDouble(),
                Is.EqualTo(beforeRequired + 3d).Within(0.0001d));
            required.Dispose();

            SetDocumentCount(harness.Documents, 1);
            Assert.That(producer.TryPeekOffer(out DocumentOffer optionalOffer), Is.True);
            Assert.That(optionalOffer.RequiresStamp, Is.False);
            Assert.That(producer.TryProduce(optionalOffer.Key, out IDocumentSession optional), Is.True);
            double beforeOptional = harness.Wallet.CurrentBalance.ToDouble();
            Assert.That(optional.TryProcess(Evaluation(SignatureEvaluationStatus.Accepted, 1f, 0.4f), true),
                Is.True);
            Assert.That(harness.Wallet.CurrentBalance.ToDouble(),
                Is.EqualTo(beforeOptional + 0.5d).Within(0.0001d));
            optional.Dispose();
        }

        [Test]
        public void OfficeDocumentOffers_ExposeExactHireAndReviewPresentationDataAndReissueOnRelease() {
            OfficeHarness harness = CreateHarness(CreateEntries(capacity: 2));
            harness.UnlockOffice();
            Assert.That(harness.Office.TryStartClerkHire(Value.One), Is.True);
            var hireProducer = new ClerkHireDocumentProducer();
            hireProducer.InitializeAsync(harness.Scope).GetAwaiter().GetResult();

            Assert.That(hireProducer.TryPeekOffer(out DocumentOffer hire), Is.True);
            Assert.That(hire.Key.Kind, Is.EqualTo(DocumentKind.ClerkHire));
            Assert.That(hire.PersonName, Is.Not.Empty);
            Assert.That(hire.PersonAge, Is.InRange(18, 65));
            Assert.That(hire.Amount, Is.EqualTo(Value.One));
            Assert.That(hire.InternalMultiplier, Is.Not.Null);
            Assert.That(hireProducer.TryProduce(hire.Key, out IDocumentSession claimedHire), Is.True);
            Assert.That(hireProducer.TryPeekOffer(out _), Is.False);
            claimedHire.Dispose();
            Assert.That(hireProducer.TryPeekOffer(out DocumentOffer reissuedHire), Is.True);
            Assert.That(reissuedHire.Key, Is.EqualTo(hire.Key));

            Assert.That(hireProducer.TryProduce(reissuedHire.Key, out IDocumentSession acceptedHire), Is.True);
            Assert.That(acceptedHire.TryProcess(Evaluation(
                SignatureEvaluationStatus.Accepted, 0.4f, 0.4f)), Is.True);
            acceptedHire.Dispose();
            int clerkId = harness.Office.Clerks[0].Id;
            Assert.That(harness.Office.TryStartSalaryReview(clerkId), Is.True);
            var reviewProducer = new ClerkSalaryReviewDocumentProducer();
            reviewProducer.InitializeAsync(harness.Scope).GetAwaiter().GetResult();

            Assert.That(reviewProducer.TryPeekOffer(out DocumentOffer review), Is.True);
            Assert.That(review.Key.Kind, Is.EqualTo(DocumentKind.ClerkSalaryReview));
            Assert.That(review.PersonName, Is.EqualTo(harness.Office.Clerks[0].Name));
            Assert.That(review.PersonAge, Is.EqualTo(harness.Office.Clerks[0].Age));
            Assert.That(review.Amount, Is.EqualTo(harness.Office.GetSalaryReviewCost(clerkId)));
            Assert.That(review.InternalMultiplier, Is.Null);
        }

        [Test]
        public void SalaryReviewProducer_ReissuesExcludesPlayerModifiersAndLosesTieToHireRegistration() {
            OfficeHarness harness = CreateHarness(CreateEntries(capacity: 2));
            harness.UnlockOffice();
            HireClerk(harness);
            int clerkId = harness.Office.Clerks[0].Id;
            Assert.That(harness.Office.TryStartSalaryReview(clerkId), Is.True);
            Value afterReviewDebit = harness.Wallet.CurrentBalance;
            Assert.That(harness.Office.TryStartClerkHire(Value.One), Is.True);
            var hireProducer = new ClerkHireDocumentProducer();
            var reviewProducer = new ClerkSalaryReviewDocumentProducer();
            hireProducer.InitializeAsync(harness.Scope).GetAwaiter().GetResult();
            reviewProducer.InitializeAsync(harness.Scope).GetAwaiter().GetResult();
            Assert.That(reviewProducer.TryProduce(out IDocumentSession released), Is.True);
            var baseRules = new SignatureDifficultyRules("base", 0.4f, 1f, 1f, 1f, null);
            var effectiveRules = baseRules with { MinimumSimilarity = 0.2f, CorridorWidthMultiplier = 2f };
            DocumentEvaluationInputs inputs = released.EvaluationPolicy.Resolve(
                new SignatureDifficultyContext(baseRules, effectiveRules));
            Assert.That(inputs.Difficulty, Is.SameAs(baseRules));
            Assert.That(inputs.Modifiers.MinimumSimilarityOffset, Is.Zero);
            released.Dispose();

            var viewModel = new DispenseViewModel(
                new IDocumentProducer[] { hireProducer, reviewProducer },
                new StaticCache<DocumentEntries>(default),
                new StableRandom(123));
            Assert.That(viewModel.TryCreateContext(viewModel.Current, out IDocumentContext context), Is.True);
            IDocumentSession selected = context.TakeSession();
            context.Dispose();
            Assert.That(selected.TryProcess(Evaluation(
                SignatureEvaluationStatus.Accepted, 0.4f, 0.4f)), Is.True);
            selected.Dispose();

            Assert.That(harness.Office.PendingHireCount, Is.Zero,
                "The hire producer must win the equal-priority registration-order tie.");
            Assert.That(harness.Office.PendingSalaryReviewCount, Is.EqualTo(1));
            Assert.That(reviewProducer.TryProduce(out IDocumentSession rejectedReview), Is.True);
            Assert.That(rejectedReview.TryProcess(Evaluation(
                SignatureEvaluationStatus.Rejected, 0.2f, 0.4f)), Is.True);
            rejectedReview.Dispose();
            Assert.That(harness.Wallet.CurrentBalance, Is.EqualTo((afterReviewDebit - Value.One).Value),
                "The rejected review is not refunded; only the subsequent hire changed the balance.");
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
            var rolls = new Queue<float>(new[] { 0.5f, 0.5f, 0.5f, 1f, 0f });
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
        public void Tick_AppliesOfficeCriticalMultiplierToAcceptedReward() {
            OfficeEntries entries = CreateEntries();
            entries.OfficeSignatureCriticalChance = 1f;
            entries.OfficeSignatureCriticalMultiplier = 4d;
            OfficeHarness harness = CreateHarness(entries, () => 1f);
            harness.UnlockOffice();
            HireClerk(harness);
            SetDocumentCount(harness.Documents, 1);
            OfficeDocumentResult result = default;
            using IDisposable subscription = harness.Office.DocumentProcessed.Subscribe(value => result = value);
            double before = harness.Wallet.CurrentBalance.ToDouble();

            harness.Office.Tick(1f);

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.RequestedReward.ToDouble(), Is.EqualTo(4d).Within(0.0001d));
            Assert.That(result.CreditedReward.ToDouble(), Is.EqualTo(4d).Within(0.0001d));
            Assert.That(harness.Wallet.CurrentBalance.ToDouble(), Is.EqualTo(before + 4d).Within(0.0001d));
        }

        [Test]
        public void Tick_MultiPayChancePaysSeparateRewardsPerAcceptedDocument() {
            OfficeEntries entries = CreateEntries();
            entries.OfficeSignatureCriticalChance = 1f;
            entries.OfficeSignatureCriticalMultiplier = 4d;
            entries.OfficeMultiPayChance = 1f;
            OfficeHarness harness = CreateHarness(entries, () => 1f);
            harness.UnlockOffice();
            HireClerk(harness);
            SetDocumentCount(harness.Documents, 1);
            OfficeDocumentResult result = default;
            using IDisposable subscription = harness.Office.DocumentProcessed.Subscribe(value => result = value);
            double before = harness.Wallet.CurrentBalance.ToDouble();

            harness.Office.Tick(1f);

            // A whole multi-pay chance of 1.0 grants a second payment; each payment rolls its own crit.
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.RequestedReward.ToDouble(), Is.EqualTo(8d).Within(0.0001d));
            Assert.That(result.CreditedReward.ToDouble(), Is.EqualTo(8d).Within(0.0001d));
            Assert.That(harness.Wallet.CurrentBalance.ToDouble(), Is.EqualTo(before + 8d).Within(0.0001d));
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
            Observable<float> updates = null, Value? income = null, IncomeEntries? incomeEntries = null,
            DocumentEntries? documentEntries = null) {
            UpgradeNodeDefinition officeUnlock = CreateUpgrade("office_unlock", FeatureIds.Office);
            UpgradeNodeDefinition documentUpgrade = CreateUpgrade("document_upgrade");
            UpgradeNodeDefinition stampUnlock = CreateUpgrade("stamp_unlock", FeatureIds.Stamp);
            var provider = new FakeAssetProvider(new[] { officeUnlock, documentUpgrade, stampUnlock });
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
            var criticalRandom = new SignatureCriticalRandomService(123UL);
            var office = new OfficeService(random, updates);
            IncomeEntries configuredIncome = incomeEntries ?? new IncomeEntries(1f, 0.4f, income ?? Value.One);

            scope.Register(wallet)
                .Register(cache, typeof(ICacheInvalidator), typeof(ICacheVersionProvider))
                .Register(upgrades)
                .Register(unlocks)
                .Register(documents)
                .Register(statistics)
                .Register(criticalRandom)
                .Register<ICacheCalculator<IncomeEntries>>(new StaticCalculator<IncomeEntries>(configuredIncome))
                .Register<ICacheCalculator<SignatureEntries>>(new StaticCalculator<SignatureEntries>(default))
                .Register<ICacheCalculator<GenerationEntries>>(new StaticCalculator<GenerationEntries>(default))
                .Register<ICacheCalculator<OfficeEntries>>(officeCalculator)
                .Register<ICacheCalculator<DocumentEntries>>(new StaticCalculator<DocumentEntries>(documentEntries ?? default))
                .Register(stash)
                .Register<IMoneyAggregator>(money)
                .Register(office);

            upgrades.InitializeAsync(scope).GetAwaiter().GetResult();
            SetAllAvailable(upgrades);
            unlocks.InitializeAsync(scope).GetAwaiter().GetResult();
            stash.PreInitializeAsync(scope).GetAwaiter().GetResult();
            office.InitializeAsync(scope).GetAwaiter().GetResult();

            var harness = new OfficeHarness(scope, wallet, cache, upgrades, unlocks, documents, statistics,
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
                OfficeSignatureCriticalChance = 0f,
                OfficeSignatureCriticalMultiplier = 1d,
                BaseClerkMultiplierMedian = 2d,
                ClerkMultiplierRangeStep = 0d,
                MinimumClerkMultiplier = 1d,
                MaximumHireSignatureMultiplier = 2d,
                SalaryReviewCostRatio = 0.5d
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
            public UnlockService Unlocks { get; }
            public DocumentGeneratorService Documents { get; }
            public GameStatisticsService Statistics { get; }
            public StaticCalculator<OfficeEntries> OfficeCalculator { get; }
            public OfficeService Office { get; }
            private bool _disposed;

            public OfficeHarness(ServiceScope scope, WalletService wallet, CacheVersionService cache,
                UpgradeService upgrades, UnlockService unlocks, DocumentGeneratorService documents,
                GameStatisticsService statistics,
                StaticCalculator<OfficeEntries> officeCalculator, OfficeService office) {
                Scope = scope;
                Wallet = wallet;
                Cache = cache;
                Upgrades = upgrades;
                Unlocks = unlocks;
                Documents = documents;
                Statistics = statistics;
                OfficeCalculator = officeCalculator;
                Office = office;
            }

            public void UnlockOffice() {
                PurchaseAndComplete(Upgrades, "office_unlock");
            }

            public void UnlockStamp() {
                PurchaseAndComplete(Upgrades, "stamp_unlock");
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
