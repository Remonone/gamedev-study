using System;
using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Documents;
using Data.Enums;
using Data.Rewards;
using R3;
using Services.Locator;
using UnityEngine;
using Utils;

namespace Services {
    /// <summary>
    /// Aggregates gameplay events into <see cref="GameStatisticsService"/> for the statistics tab.
    /// Buffered rate/currency statistics are flushed at most once per second through a single batch
    /// so hot paths (office ticks, bank payouts) do not spam Changed events. The successful-signature
    /// total is authoritative in GameStatisticsService and is updated synchronously on acceptance.
    /// Money totals use a reversible finite-double encoding because statistics store doubles.
    /// </summary>
    public sealed class StatisticsTrackingService : IService, IInitialize, IPostInitialize {
        private const int RateWindowSeconds = 5;
        private const int FlushMutationCount = 10;

        private const int SnapshotMutationCount = 13;

        private readonly CompositeDisposable _subscriptions = new();
        private readonly Observable<float> _updateStream;
        private readonly GameStatisticMutation[] _flushMutations = new GameStatisticMutation[FlushMutationCount];
        private readonly GameStatisticMutation[] _snapshotMutations = new GameStatisticMutation[SnapshotMutationCount];

        private readonly Value[] _incomeBuckets = new Value[RateWindowSeconds];
        private readonly double[] _generatedBuckets = new double[RateWindowSeconds];
        private readonly double[] _consumedBuckets = new double[RateWindowSeconds];
        private readonly double[] _processedBuckets = new double[RateWindowSeconds];

        private GameStatisticsService _statistics;
        private PlayerStatStash _stash;
        private WalletService _wallet;
        private SaveService _saveService;
        private bool _updateSubscribed;

        private Value _totalEarned;
        private Value _totalSpent;
        private Value _maxBalance;
        private double _generatedTotal;
        private double _consumedTotal;
        private double _billAcceptedCount;
        private double _lastProcessedTotal;
        private double _secondAccumulator;

        public StatisticsTrackingService() : this(null) { }

        internal StatisticsTrackingService(Observable<float> updateStream) {
            _updateStream = updateStream;
        }

        public UniTask InitializeAsync(IServiceScope scope) {
            _statistics = scope.Get<GameStatisticsService>();
            _stash = scope.Get<PlayerStatStash>();
            _wallet = scope.Get<WalletService>();
            _saveService = scope.Get<SaveService>();
            DocumentGeneratorService generator = scope.Get<DocumentGeneratorService>();
            PlayerSignatureAcceptor signatureAcceptor = scope.Get<PlayerSignatureAcceptor>();
            CacheVersionService cacheVersions = scope.Get<CacheVersionService>();

            _wallet.Credited.Subscribe(OnMoneyCredited).AddTo(_subscriptions);
            _wallet.Debited.Subscribe(OnMoneyDebited).AddTo(_subscriptions);
            _wallet.BalanceChanged.Subscribe(_ => TrackMaxBalance()).AddTo(_subscriptions);
            generator.DocumentsGenerated.Subscribe(OnDocumentsGenerated).AddTo(_subscriptions);
            generator.DocumentsConsumed.Subscribe(OnDocumentsConsumed).AddTo(_subscriptions);
            signatureAcceptor.DocumentResults.Subscribe(OnDocumentHandled).AddTo(_subscriptions);
            cacheVersions.Invalidated.Where(IsSnapshotCacheType).Subscribe(_ => PushCacheSnapshots())
                .AddTo(_subscriptions);
            _saveService.BeforeSnapshot += FlushStatistics;
            return UniTask.CompletedTask;
        }

        public UniTask PostInitializeAsync(IServiceScope scope) {
            SeedFromRestoredStatistics();
            PushCacheSnapshots();
            FlushStatistics();
            if (_updateSubscribed) return UniTask.CompletedTask;

            _updateSubscribed = true;
            Observable<float> stream = _updateStream ?? Observable.EveryUpdate().Select(_ => Time.deltaTime);
            stream.Subscribe(OnUpdate).AddTo(_subscriptions);
            return UniTask.CompletedTask;
        }

        public void Dispose() {
            if (_saveService != null) _saveService.BeforeSnapshot -= FlushStatistics;
            _subscriptions.Dispose();
        }

        private void OnMoneyCredited(Value credited) {
            _totalEarned = SaturatingAdd(_totalEarned, credited);
            _incomeBuckets[0] = SaturatingAdd(_incomeBuckets[0], credited);
            TrackMaxBalance();
        }

        private void OnMoneyDebited(Value debited) {
            _totalSpent = SaturatingAdd(_totalSpent, debited);
        }

        private void OnDocumentsGenerated(int count) {
            int actual = Math.Max(0, count);
            _generatedTotal += actual;
            _generatedBuckets[0] += actual;
        }

        private void OnDocumentsConsumed(int count) {
            int actual = Math.Max(0, count);
            _consumedTotal += actual;
            _consumedBuckets[0] += actual;
        }

        private void OnDocumentHandled(DocumentHandleResult result) {
            if (result.Status != RewardStatus.RewardGranted) return;

            _statistics.AddValue(GameStatisticIds.DocumentsSuccessfullySigned, 1d);
            if (result.Kind == DocumentKind.Bill) _billAcceptedCount++;
        }

        private void OnUpdate(float deltaTime) {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f) return;

            _secondAccumulator += deltaTime;
            int elapsedSeconds = 0;
            while (_secondAccumulator >= 1d && elapsedSeconds < RateWindowSeconds) {
                _secondAccumulator -= 1d;
                elapsedSeconds++;
            }

            if (_secondAccumulator >= 1d) _secondAccumulator = 0d;
            for (int index = 0; index < elapsedSeconds; index++) RollBuckets();
            if (elapsedSeconds > 0) FlushStatistics();
        }

        private void RollBuckets() {
            // Sample the office processing delta last so the closing second is complete.
            TrackProcessedDelta();

            for (int index = RateWindowSeconds - 1; index > 0; index--) {
                _generatedBuckets[index] = _generatedBuckets[index - 1];
                _consumedBuckets[index] = _consumedBuckets[index - 1];
                _processedBuckets[index] = _processedBuckets[index - 1];
                _incomeBuckets[index] = _incomeBuckets[index - 1];
            }

            _generatedBuckets[0] = 0d;
            _consumedBuckets[0] = 0d;
            _processedBuckets[0] = 0d;
            _incomeBuckets[0] = Value.Zero;
        }

        private void TrackProcessedDelta() {
            if (_statistics.TryGetValue(GameStatisticIds.OfficeProcessedDocuments, out double current)) {
                double delta = current - _lastProcessedTotal;
                if (delta > 0d) _processedBuckets[0] += delta;
                _lastProcessedTotal = current;
            }
            else {
                _lastProcessedTotal = 0d;
            }
        }

        private void FlushStatistics() {
            TrackMaxBalance();
            Value incomeRate = SumIncomeBuckets() / RateWindowSeconds;
            int count = 0;
            _flushMutations[count++] = GameStatisticMutation.Set(
                GameStatisticIds.MoneyTotalEarned, GameStatisticFormats.EncodeMoney(_totalEarned));
            _flushMutations[count++] = GameStatisticMutation.Set(
                GameStatisticIds.MoneyTotalSpent, GameStatisticFormats.EncodeMoney(_totalSpent));
            _flushMutations[count++] = GameStatisticMutation.Set(
                GameStatisticIds.MoneyMaxBalance, GameStatisticFormats.EncodeMoney(_maxBalance));
            _flushMutations[count++] = GameStatisticMutation.Set(
                GameStatisticIds.MoneyIncomePerSecond, GameStatisticFormats.EncodeMoney(incomeRate));
            _flushMutations[count++] = GameStatisticMutation.Set(
                GameStatisticIds.DocumentsGeneratedTotal, _generatedTotal);
            _flushMutations[count++] = GameStatisticMutation.Set(
                GameStatisticIds.DocumentsConsumedTotal, _consumedTotal);
            _flushMutations[count++] = GameStatisticMutation.Set(
                GameStatisticIds.BillsAcceptedCount, _billAcceptedCount);
            _flushMutations[count++] = GameStatisticMutation.Set(
                GameStatisticIds.DocumentsGeneratedPerSecond, SumBuckets(_generatedBuckets) / RateWindowSeconds);
            _flushMutations[count++] = GameStatisticMutation.Set(
                GameStatisticIds.DocumentsConsumedPerSecond, SumBuckets(_consumedBuckets) / RateWindowSeconds);
            _flushMutations[count++] = GameStatisticMutation.Set(
                GameStatisticIds.DocumentsProcessedPerSecond, SumBuckets(_processedBuckets) / RateWindowSeconds);
            _statistics.ApplyBatch(_flushMutations, count);
        }

        private void TrackMaxBalance() {
            Value balance = _wallet.CurrentBalance;
            if (balance > _maxBalance) _maxBalance = balance;
        }

        private void SeedFromRestoredStatistics() {
            _totalEarned = ReadMoneyStatistic(GameStatisticIds.MoneyTotalEarned);
            _totalSpent = ReadMoneyStatistic(GameStatisticIds.MoneyTotalSpent);
            _maxBalance = ReadMoneyStatistic(GameStatisticIds.MoneyMaxBalance);
            _generatedTotal = ReadCountStatistic(GameStatisticIds.DocumentsGeneratedTotal);
            _consumedTotal = ReadCountStatistic(GameStatisticIds.DocumentsConsumedTotal);
            _billAcceptedCount = ReadCountStatistic(GameStatisticIds.BillsAcceptedCount);
            _lastProcessedTotal = ReadCountStatistic(GameStatisticIds.OfficeProcessedDocuments);
            TrackMaxBalance();
        }

        private Value ReadMoneyStatistic(string statisticId) {
            return _statistics.TryGetValue(statisticId, out double encoded) &&
                   GameStatisticFormats.TryDecodeMoney(encoded, out Value value)
                ? value
                : Value.Zero;
        }

        private double ReadCountStatistic(string statisticId) {
            return _statistics.TryGetValue(statisticId, out double value) && value > 0d ? value : 0d;
        }

        private void PushCacheSnapshots() {
            IncomeEntries income = default;
            OfficeEntries office = default;
            BankEntries bank = default;
            bool hasIncome = _stash.IncomeData != null;
            bool hasOffice = _stash.OfficeData != null;
            bool hasBank = _stash.BankData != null;
            if (hasIncome) income = _stash.IncomeData.Value;
            if (hasOffice) office = _stash.OfficeData.Value;
            if (hasBank) bank = _stash.BankData.Value;

            var mutations = _snapshotMutations;
            int count = 0;
            mutations[count++] = GameStatisticMutation.Set(
                GameStatisticIds.IncomeSignatureIncome,
                GameStatisticFormats.EncodeMoney(hasIncome ? income.IncomePerDocument : Value.Zero));
            mutations[count++] = GameStatisticMutation.Set(
                GameStatisticIds.IncomeClerkIncome, hasOffice ? office.RewardMultiplier : 0d);
            mutations[count++] = GameStatisticMutation.Set(
                GameStatisticIds.IncomeBankIncome,
                GameStatisticFormats.EncodeMoney(hasBank ? bank.PayoutAmount : Value.Zero));
            mutations[count++] = GameStatisticMutation.Set(
                GameStatisticIds.CritSignatureChance, hasIncome ? income.ManualSignatureCriticalChance : 0f);
            mutations[count++] = GameStatisticMutation.Set(
                GameStatisticIds.CritSignatureMultiplier, hasIncome ? income.ManualSignatureCriticalMultiplier : 0d);
            mutations[count++] = GameStatisticMutation.Set(
                GameStatisticIds.CritClerkChance, hasOffice ? office.OfficeSignatureCriticalChance : 0f);
            mutations[count++] = GameStatisticMutation.Set(
                GameStatisticIds.CritClerkMultiplier, hasOffice ? office.OfficeSignatureCriticalMultiplier : 0d);
            mutations[count++] = GameStatisticMutation.Set(
                GameStatisticIds.CritBankChance, hasBank ? bank.CriticalChance : 0f);
            mutations[count++] = GameStatisticMutation.Set(
                GameStatisticIds.CritBankMultiplier, hasBank ? bank.CriticalMultiplier : 0d);
            mutations[count++] = GameStatisticMutation.Set(
                GameStatisticIds.MultiPaySignatureChance, hasIncome ? income.ManualSignatureMultiPayChance : 0f);
            mutations[count++] = GameStatisticMutation.Set(
                GameStatisticIds.MultiPayClerkChance, hasOffice ? office.OfficeMultiPayChance : 0f);
            mutations[count++] = GameStatisticMutation.Set(
                GameStatisticIds.MultiPayBankChance, hasBank ? bank.MultiPayChance : 0f);
            mutations[count++] = GameStatisticMutation.Set(
                GameStatisticIds.OfficeClerkCapacity, hasOffice ? office.ClerkCapacity : 0d);
            _statistics.ApplyBatch(mutations, count);
        }

        private static bool IsSnapshotCacheType(Type cacheType) {
            return cacheType == typeof(IncomeEntries) ||
                   cacheType == typeof(OfficeEntries) ||
                   cacheType == typeof(BankEntries);
        }

        private Value SumIncomeBuckets() {
            Value sum = Value.Zero;
            for (int index = 0; index < RateWindowSeconds; index++) {
                sum = SaturatingAdd(sum, _incomeBuckets[index]);
            }

            return sum;
        }

        private static double SumBuckets(double[] buckets) {
            double sum = 0d;
            for (int index = 0; index < buckets.Length; index++) sum += buckets[index];
            return sum;
        }

        private static Value SaturatingAdd(Value first, Value second) {
            if (first.IsZero) return second;
            if (second.IsZero) return first;
            if (first == Value.Infinity || second == Value.Infinity) return Value.Infinity;
            return first + second;
        }

    }
}
