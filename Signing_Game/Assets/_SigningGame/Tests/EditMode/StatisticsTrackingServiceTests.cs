using System;
using System.Collections.Generic;
using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Documents;
using Data.Enums;
using Data.Persistence;
using Data.Rewards;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using R3;
using Services;
using Services.Locator;
using Utils;

namespace Tests.EditMode {
    public sealed class StatisticsTrackingServiceTests {
        private readonly List<IDisposable> _disposables = new();

        [TearDown]
        public void TearDown() {
            for (int index = _disposables.Count - 1; index >= 0; index--) _disposables[index].Dispose();
            _disposables.Clear();
        }

        [Test]
        public void WalletOperations_AccumulateTotalsAndMaxBalance() {
            TrackingHarness harness = CreateHarness();
            harness.Wallet.ReplenishWallet(new Value(100d));

            harness.PumpSeconds(1f);

            AssertMoneyStatistic(harness.Statistics, GameStatisticIds.MoneyTotalEarned, new Value(100d));
            AssertMoneyStatistic(harness.Statistics, GameStatisticIds.MoneyMaxBalance, new Value(100d));
            Assert.That(harness.Statistics.TryGetValue(GameStatisticIds.MoneyTotalSpent, out double spent) &&
                       spent == 0d, Is.True);

            Assert.That(harness.Wallet.TryWithdrawWallet(new Value(40d)), Is.True);
            harness.Wallet.ReplenishWallet(new Value(1000d));
            harness.PumpSeconds(1f);

            AssertMoneyStatistic(harness.Statistics, GameStatisticIds.MoneyTotalSpent, new Value(40d));
            AssertMoneyStatistic(harness.Statistics, GameStatisticIds.MoneyTotalEarned, new Value(1100d));
            AssertMoneyStatistic(harness.Statistics, GameStatisticIds.MoneyMaxBalance, new Value(1060d));
        }

        [Test]
        public void WalletEvents_SkipInsignificantOperations() {
            TrackingHarness harness = CreateHarness();
            harness.Wallet.ReplenishWallet(Value.FromLog10(15d));
            harness.PumpSeconds(1f);

            AssertMoneyStatistic(harness.Statistics, GameStatisticIds.MoneyTotalEarned, Value.FromLog10(15d));

            // A credit a billion times smaller than the balance is insignificant and
            // must not change the earned total.
            harness.Wallet.ReplenishWallet(Value.One);
            harness.PumpSeconds(1f);

            AssertMoneyStatistic(harness.Statistics, GameStatisticIds.MoneyTotalEarned, Value.FromLog10(15d));
        }

        [Test]
        public void SilentWalletTransactions_PreserveIntermediateMaximumBalance() {
            TrackingHarness harness = CreateHarness();

            Assert.That(harness.Wallet.ReplenishWallet(new Value(100d), false), Is.True);
            Assert.That(harness.Wallet.TryWithdrawWallet(new Value(90d), false), Is.True);
            harness.PumpSeconds(1f);

            AssertMoneyStatistic(harness.Statistics, GameStatisticIds.MoneyMaxBalance, new Value(100d));
        }

        [Test]
        public void SuccessfulPlayerSignatures_AreCounted_AndBillsTrackedSeparately() {
            TrackingHarness harness = CreateHarness();
            ReportResult(harness.SignatureAcceptor, DocumentKind.Normal, RewardStatus.RewardGranted);
            ReportResult(harness.SignatureAcceptor, DocumentKind.Upgrade, RewardStatus.RewardGranted);
            ReportResult(harness.SignatureAcceptor, DocumentKind.Bill, RewardStatus.RewardGranted);
            ReportResult(harness.SignatureAcceptor, DocumentKind.Normal, RewardStatus.RewardRejected);

            AssertStatistic(harness.Statistics, GameStatisticIds.DocumentsSuccessfullySigned, 3d);
            harness.PumpSeconds(1f);

            AssertStatistic(harness.Statistics, GameStatisticIds.DocumentsSuccessfullySigned, 3d);
            AssertStatistic(harness.Statistics, GameStatisticIds.BillsAcceptedCount, 1d);

            ReportResult(harness.SignatureAcceptor, DocumentKind.Normal, RewardStatus.RewardGranted);
            AssertStatistic(harness.Statistics, GameStatisticIds.DocumentsSuccessfullySigned, 4d);
            harness.PumpSeconds(1f);
            AssertStatistic(harness.Statistics, GameStatisticIds.DocumentsSuccessfullySigned, 4d);
        }

        [Test]
        public void GeneratedDocuments_TrackActualInventoryAddedAndRate() {
            TrackingHarness harness = CreateHarness(generation: new GenerationEntries(10f, 3));

            InvokeGeneratorUpdate(harness.Generator, 1f);
            harness.PumpSeconds(1f);

            Assert.That(harness.Generator.DocumentQuantity, Is.EqualTo(4));
            AssertStatistic(harness.Statistics, GameStatisticIds.DocumentsGeneratedTotal, 3d);
            AssertStatistic(harness.Statistics, GameStatisticIds.DocumentsGeneratedPerSecond, 0.6d);
        }

        [Test]
        public void GenerationCapacity_IncludesActiveReservations() {
            TrackingHarness harness = CreateHarness(generation: new GenerationEntries(10f, 1));
            SetDocumentCount(harness.Generator, int.MaxValue);
            Assert.That(harness.Generator.TryReserveDocument(
                out DocumentGeneratorService.DocumentReservation reservation), Is.True);

            InvokeGeneratorUpdate(harness.Generator, 1f);

            Assert.That(harness.Generator.TryCancelReservation(reservation), Is.True);
            Assert.That(harness.Generator.DocumentQuantity, Is.EqualTo(int.MaxValue));
            JObject saved = (JObject)harness.Generator.Serialize();
            Assert.That(saved["documentQuantity"]?.Value<long>(), Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void ExtremeGeneration_KeepsFiniteRemainderAndClampsInventory() {
            TrackingHarness harness = CreateHarness(
                generation: new GenerationEntries(float.MaxValue, int.MaxValue));

            InvokeGeneratorUpdate(harness.Generator, float.MaxValue);

            Assert.That(harness.Generator.DocumentQuantity, Is.EqualTo(int.MaxValue));
            JObject saved = (JObject)harness.Generator.Serialize();
            double remainder = saved["currentPoints"].Value<double>();
            Assert.That(double.IsNaN(remainder) || double.IsInfinity(remainder), Is.False);
            Assert.That(remainder, Is.GreaterThanOrEqualTo(0d).And.LessThan(DocumentGeneratorService.PointsPerDocument));
        }

        [Test]
        public void ObtainedDocuments_AccumulateConsumedTotal() {
            TrackingHarness harness = CreateHarness();
            SetDocumentCount(harness.Generator, 5);
            Assert.That(harness.Generator.TryObtainDocument(), Is.True);
            Assert.That(harness.Generator.TryObtainDocument(), Is.True);

            harness.PumpSeconds(1f);

            AssertStatistic(harness.Statistics, GameStatisticIds.DocumentsConsumedTotal, 2d);
        }

        [Test]
        public void ReservedThenCancelledDocuments_DoNotCountAsConsumed() {
            TrackingHarness harness = CreateHarness();
            SetDocumentCount(harness.Generator, 3);
            Assert.That(harness.Generator.TryReserveDocument(
                out DocumentGeneratorService.DocumentReservation reservation), Is.True);
            Assert.That(harness.Generator.TryCancelReservation(reservation), Is.True);

            harness.PumpSeconds(1f);

            Assert.That(harness.Statistics.TryGetValue(
                GameStatisticIds.DocumentsConsumedTotal, out double consumed) && consumed == 0d, Is.True);
        }

        [Test]
        public void RateWindow_ComputesPerSecondAverages() {
            TrackingHarness harness = CreateHarness();
            SetDocumentCount(harness.Generator, 10);
            harness.Wallet.ReplenishWallet(new Value(10d));
            Assert.That(harness.Generator.TryObtainDocument(), Is.True);
            Assert.That(harness.Generator.TryObtainDocument(), Is.True);
            Assert.That(harness.Generator.TryObtainDocument(), Is.True);
            Assert.That(harness.Generator.TryObtainDocument(), Is.True);
            Assert.That(harness.Generator.TryObtainDocument(), Is.True);
            harness.Statistics.AddValue(GameStatisticIds.OfficeProcessedDocuments, 10d);
            harness.PumpSeconds(1f);

            AssertMoneyStatistic(harness.Statistics, GameStatisticIds.MoneyIncomePerSecond, new Value(2d));
            AssertStatistic(harness.Statistics, GameStatisticIds.DocumentsConsumedPerSecond, 1d);
            AssertStatistic(harness.Statistics, GameStatisticIds.DocumentsProcessedPerSecond, 2d);

            // Second without activity keeps averaging over the five second window.
            harness.PumpSeconds(1f);
            AssertStatistic(harness.Statistics, GameStatisticIds.DocumentsConsumedPerSecond, 1d);

            // After the active second falls out of the window the rate decays to zero.
            harness.PumpSeconds(4f);
            AssertStatistic(harness.Statistics, GameStatisticIds.DocumentsConsumedPerSecond, 0d);
        }

        [Test]
        public void RateWindow_HandlesLargeDeltaTimesWithoutOverflow() {
            TrackingHarness harness = CreateHarness();
            harness.Wallet.ReplenishWallet(new Value(10d));
            harness.UpdateSubject.OnNext(60f);

            // A 60 second frame rolls at most the whole window: the credit falls out
            // of the window and the rate reports zero instead of spiking.
            AssertStatistic(harness.Statistics, GameStatisticIds.MoneyIncomePerSecond, 0d);
        }

        [Test]
        public void CacheSnapshots_PushEntriesAndRefreshOnInvalidation() {
            var income = new IncomeEntries {
                IncomePerDocument = new Value(5d),
                ManualSignatureCriticalChance = 0.25f,
                ManualSignatureCriticalMultiplier = 3d,
                ManualSignatureMultiPayChance = 40f
            };
            var office = new OfficeEntries {
                ClerkCapacity = 4,
                RewardMultiplier = 0.5f,
                OfficeSignatureCriticalChance = 0.1f,
                OfficeSignatureCriticalMultiplier = 2d,
                OfficeMultiPayChance = 100f
            };
            var bank = new BankEntries {
                PayoutAmount = new Value(7d),
                CriticalChance = 0.05f,
                CriticalMultiplier = 4d,
                MultiPayChance = 12.5f
            };
            TrackingHarness harness = CreateHarness(income, office, bank);

            AssertMoneyStatistic(harness.Statistics, GameStatisticIds.IncomeSignatureIncome, new Value(5d));
            AssertStatistic(harness.Statistics, GameStatisticIds.IncomeClerkIncome, 0.5d);
            AssertMoneyStatistic(harness.Statistics, GameStatisticIds.IncomeBankIncome, new Value(7d));
            AssertStatistic(harness.Statistics, GameStatisticIds.CritSignatureChance, 0.25d);
            AssertStatistic(harness.Statistics, GameStatisticIds.CritSignatureMultiplier, 3d);
            AssertStatistic(harness.Statistics, GameStatisticIds.CritClerkChance, 0.1d);
            AssertStatistic(harness.Statistics, GameStatisticIds.CritClerkMultiplier, 2d);
            AssertStatistic(harness.Statistics, GameStatisticIds.CritBankChance, 0.05d);
            AssertStatistic(harness.Statistics, GameStatisticIds.CritBankMultiplier, 4d);
            AssertStatistic(harness.Statistics, GameStatisticIds.MultiPaySignatureChance, 40d);
            AssertStatistic(harness.Statistics, GameStatisticIds.MultiPayClerkChance, 100d);
            AssertStatistic(harness.Statistics, GameStatisticIds.MultiPayBankChance, 12.5d);
            AssertStatistic(harness.Statistics, GameStatisticIds.OfficeClerkCapacity, 4d);

            harness.IncomeCalculator.Value = new IncomeEntries {
                IncomePerDocument = new Value(50d),
                ManualSignatureCriticalChance = 0.75f,
                ManualSignatureCriticalMultiplier = 6d,
                ManualSignatureMultiPayChance = 0f
            };
            ((ICacheInvalidator)harness.Cache).Invalidate<IncomeEntries>();

            AssertMoneyStatistic(harness.Statistics, GameStatisticIds.IncomeSignatureIncome, new Value(50d));
            AssertStatistic(harness.Statistics, GameStatisticIds.CritSignatureChance, 0.75d);
            AssertStatistic(harness.Statistics, GameStatisticIds.CritSignatureMultiplier, 6d);
            AssertStatistic(harness.Statistics, GameStatisticIds.MultiPaySignatureChance, 0d);
        }

        [Test]
        public void RestoredStatistics_SeedTotalsAndCounters() {
            var restored = new JObject {
                ["money.total_earned"] = GameStatisticFormats.EncodeMoney(new Value(100d)),
                ["money.total_spent"] = GameStatisticFormats.EncodeMoney(new Value(30d)),
                ["money.max_balance"] = GameStatisticFormats.EncodeMoney(new Value(200d)),
                ["documents.generated_total"] = 7d,
                ["documents.consumed_total"] = 6d,
                ["documents.successfully_signed"] = 4d,
                ["bills.accepted_count"] = 2d,
                ["office.processed_documents"] = 500d
            };
            TrackingHarness harness = CreateHarness(restoredStatistics: restored);
            harness.Wallet.ReplenishWallet(new Value(50d));
            ReportResult(harness.SignatureAcceptor, DocumentKind.Bill, RewardStatus.RewardGranted);
            harness.PumpSeconds(1f);

            AssertMoneyStatistic(harness.Statistics, GameStatisticIds.MoneyTotalEarned, new Value(150d));
            AssertMoneyStatistic(harness.Statistics, GameStatisticIds.MoneyTotalSpent, new Value(30d));
            AssertMoneyStatistic(harness.Statistics, GameStatisticIds.MoneyMaxBalance, new Value(200d));
            AssertStatistic(harness.Statistics, GameStatisticIds.DocumentsGeneratedTotal, 7d);
            AssertStatistic(harness.Statistics, GameStatisticIds.DocumentsConsumedTotal, 6d);
            AssertStatistic(harness.Statistics, GameStatisticIds.DocumentsSuccessfullySigned, 5d);
            AssertStatistic(harness.Statistics, GameStatisticIds.BillsAcceptedCount, 3d);
        }

        [Test]
        public void RestoredProcessedBaseline_DoesNotDoubleCountOldDocuments() {
            var restored = new JObject {
                ["office.processed_documents"] = 500d
            };
            TrackingHarness harness = CreateHarness(restoredStatistics: restored);
            harness.Statistics.AddValue(GameStatisticIds.OfficeProcessedDocuments, 5d);
            harness.PumpSeconds(1f);

            AssertStatistic(harness.Statistics, GameStatisticIds.DocumentsProcessedPerSecond, 1d);
        }

        [Test]
        public void ExtremeMagnitudes_RoundTripThroughLog10() {
            TrackingHarness harness = CreateHarness();
            harness.Wallet.ReplenishWallet(Value.FromLog10(300d));
            harness.PumpSeconds(1f);

            Assert.That(harness.Statistics.TryGetValue(
                GameStatisticIds.MoneyTotalEarned, out double log10), Is.True);
            Assert.That(log10, Is.EqualTo(301d).Within(0.0001d));
            Assert.That(GameStatisticFormats.TryDecodeMoney(log10, out Value decoded), Is.True);
            Assert.That(decoded > Value.FromLog10(299.9d), Is.True);
        }

        [Test]
        public void SerializeDeserialize_RoundTripsTrackedCounters() {
            TrackingHarness harness = CreateHarness();
            SetDocumentCount(harness.Generator, 3);
            harness.Wallet.ReplenishWallet(new Value(25d));
            Assert.That(harness.Generator.TryObtainDocument(), Is.True);
            ReportResult(harness.SignatureAcceptor, DocumentKind.Normal, RewardStatus.RewardGranted);
            harness.PumpSeconds(1f);

            JToken state = harness.Statistics.Serialize();

            var restoredStatistics = new GameStatisticsService();
            restoredStatistics.Deserialize(state);
            var restoredScope = new ServiceScope(null);
            restoredScope.Register(restoredStatistics);
            _disposables.Add(restoredScope);

            Assert.That(restoredStatistics.TryGetValue(GameStatisticIds.MoneyTotalEarned, out double earned),
                Is.True);
            Assert.That(earned, Is.EqualTo(GameStatisticFormats.EncodeMoney(new Value(25d))).Within(0.0001d));
            Assert.That(restoredStatistics.TryGetValue(GameStatisticIds.DocumentsConsumedTotal,
                out double consumed), Is.True);
            Assert.That(consumed, Is.EqualTo(1d));
            Assert.That(restoredStatistics.TryGetValue(GameStatisticIds.DocumentsSuccessfullySigned,
                out double signed), Is.True);
            Assert.That(signed, Is.EqualTo(1d));
        }

        [Test]
        public void SnapshotImmediatelyAfterEvents_FlushesAndRestoresStatistics() {
            TrackingHarness harness = CreateHarness();
            SetDocumentCount(harness.Generator, 2);
            harness.Wallet.ReplenishWallet(new Value(25d));
            Assert.That(harness.Generator.TryObtainDocument(), Is.True);
            ReportResult(harness.SignatureAcceptor, DocumentKind.Normal, RewardStatus.RewardGranted);

            SaveSnapshot snapshot = harness.Save.CreateSnapshot();
            var restored = new GameStatisticsService();
            restored.Deserialize(snapshot.Sections["GameStatistics"]);

            AssertMoneyStatistic(restored, GameStatisticIds.MoneyTotalEarned, new Value(25d));
            AssertStatistic(restored, GameStatisticIds.DocumentsConsumedTotal, 1d);
            AssertStatistic(restored, GameStatisticIds.DocumentsSuccessfullySigned, 1d);
        }

        [Test]
        public void Bills_WithoutCompletions_ReportZeroAcceptedCount() {
            TrackingHarness harness = CreateHarness();

            harness.PumpSeconds(1f);

            AssertStatistic(harness.Statistics, GameStatisticIds.BillsAcceptedCount, 0d);
        }

        private static void AssertStatistic(GameStatisticsService statistics, string id, double expected) {
            Assert.That(statistics.TryGetValue(id, out double value), Is.True, id);
            Assert.That(value, Is.EqualTo(expected).Within(0.0001d), id);
        }

        private static void AssertMoneyStatistic(GameStatisticsService statistics, string id, Value expected) {
            AssertStatistic(statistics, id, GameStatisticFormats.EncodeMoney(expected));
        }

        private static void InvokeGeneratorUpdate(DocumentGeneratorService generator, float deltaTime) {
            typeof(DocumentGeneratorService)
                .GetMethod("OnUpdate", System.Reflection.BindingFlags.Instance |
                                       System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(generator, new object[] { deltaTime });
        }

        private static void ReportResult(
            PlayerSignatureAcceptor acceptor,
            DocumentKind kind,
            RewardStatus status) {
            var field = typeof(PlayerSignatureAcceptor).GetField(
                "_documentResults",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var subject = (Subject<DocumentHandleResult>)field?.GetValue(acceptor);
            Assert.That(subject, Is.Not.Null);
            subject.OnNext(new DocumentHandleResult(kind, status, 1f));
        }

        private static void SetDocumentCount(DocumentGeneratorService documents, int count) {
            documents.Deserialize(new JObject {
                ["documentQuantity"] = count,
                ["currentPoints"] = 0f
            });
        }

        private TrackingHarness CreateHarness(
            IncomeEntries? income = null,
            OfficeEntries? office = null,
            BankEntries? bank = null,
            GenerationEntries? generation = null,
            JToken restoredStatistics = null) {
            var statistics = new GameStatisticsService();
            if (restoredStatistics != null) statistics.Deserialize(restoredStatistics);
            var cache = new CacheVersionService();
            var wallet = new WalletService();
            var incomeCalculator = new MutableCalculator<IncomeEntries>(income ?? default);
            var officeCalculator = new MutableCalculator<OfficeEntries>(office ?? default);
            MutableCalculator<BankEntries> bankCalculator = bank.HasValue
                ? new MutableCalculator<BankEntries>(bank.Value)
                : null;
            var stash = new PlayerStatStash();
            var generator = new DocumentGeneratorService();
            var accepted = new AcceptedNormalDocumentService();
            var bills = new BillService();
            var signatureAcceptor = new PlayerSignatureAcceptor();
            var updateSubject = new Subject<float>();
            var tracking = new StatisticsTrackingService(updateSubject);
            var save = new SaveService(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"statistics-{Guid.NewGuid():N}.json"),
                loadExistingOnInitialize: false);

            var scope = new ServiceScope(null);
            scope.Register(save)
                .Register(statistics)
                .Register(cache, typeof(ICacheInvalidator), typeof(ICacheVersionProvider))
                .Register<ICacheCalculator<IncomeEntries>>(incomeCalculator)
                .Register<ICacheCalculator<SignatureEntries>>(
                    new StaticCalculator<SignatureEntries>(default))
                .Register<ICacheCalculator<GenerationEntries>>(
                    new StaticCalculator<GenerationEntries>(generation ?? default))
                .Register<ICacheCalculator<OfficeEntries>>(officeCalculator)
                .Register<ICacheCalculator<DocumentEntries>>(
                    new StaticCalculator<DocumentEntries>(default));
            if (bankCalculator != null) {
                scope.Register<ICacheCalculator<BankEntries>>(bankCalculator);
            }

            scope.Register(stash)
                .Register(wallet)
                .Register(generator)
                .Register(accepted)
                .Register(bills)
                .Register(signatureAcceptor)
                .Register(tracking);

            save.PreInitializeAsync(scope).GetAwaiter().GetResult();
            stash.PreInitializeAsync(scope).GetAwaiter().GetResult();
            generator.InitializeAsync(scope).GetAwaiter().GetResult();
            tracking.InitializeAsync(scope).GetAwaiter().GetResult();
            tracking.PostInitializeAsync(scope).GetAwaiter().GetResult();

            var harness = new TrackingHarness(scope, save, statistics, wallet, cache, generator, accepted,
                signatureAcceptor, incomeCalculator, updateSubject);
            _disposables.Add(harness);
            return harness;
        }

        private sealed class TrackingHarness : IDisposable {
            private bool _disposed;

            public TrackingHarness(
                ServiceScope scope,
                SaveService save,
                GameStatisticsService statistics,
                WalletService wallet,
                CacheVersionService cache,
                DocumentGeneratorService generator,
                AcceptedNormalDocumentService accepted,
                PlayerSignatureAcceptor signatureAcceptor,
                MutableCalculator<IncomeEntries> incomeCalculator,
                Subject<float> updateSubject) {
                Scope = scope;
                Save = save;
                Statistics = statistics;
                Wallet = wallet;
                Cache = cache;
                Generator = generator;
                Accepted = accepted;
                SignatureAcceptor = signatureAcceptor;
                IncomeCalculator = incomeCalculator;
                UpdateSubject = updateSubject;
            }

            public ServiceScope Scope { get; }
            public SaveService Save { get; }
            public GameStatisticsService Statistics { get; }
            public WalletService Wallet { get; }
            public CacheVersionService Cache { get; }
            public DocumentGeneratorService Generator { get; }
            public AcceptedNormalDocumentService Accepted { get; }
            public PlayerSignatureAcceptor SignatureAcceptor { get; }
            public MutableCalculator<IncomeEntries> IncomeCalculator { get; }
            public Subject<float> UpdateSubject { get; }

            public void PumpSeconds(float seconds) {
                UpdateSubject.OnNext(seconds);
            }

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
    }
}
