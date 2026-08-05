using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Enums;
using Data.Office;
using Data.Results;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using R3;
using Services.Calculators;
using Services.Locator;
using UnityEngine;
using Utils;

namespace Services {
    public sealed class OfficeService : IService, IInitialize, IPostInitialize, ISaveable {
        public const int MaxDocumentsProcessedPerTick = 256;

        private const double MaximumValueLog10 = (double)int.MaxValue * 3d;

        private readonly List<OfficeClerkState> _clerks = new();
        private readonly ReadOnlyCollection<OfficeClerkState> _readOnlyClerks;
        private readonly List<PendingClerkHireRecord> _pendingHires = new();
        private readonly Dictionary<long, long> _activeHireClaims = new();
        private readonly HashSet<long> _claimedHireRequestIds = new();
        private readonly double[] _work = new double[OfficeCacheCalculator.MaximumClerkCapacity];
        private readonly GameStatisticMutation[] _tickStatisticMutations = new GameStatisticMutation[3];
        private readonly GameStatisticMutation[] _singleStatisticMutation = new GameStatisticMutation[1];
        private readonly Subject<Unit> _changed = new();
        private readonly Subject<OfficeDocumentResult> _documentProcessed = new();
        private readonly CompositeDisposable _subscriptions = new();
        private readonly Func<float> _randomValue;
        private readonly Observable<float> _updateStream;

        private UnlockService _unlocks;
        private DocumentGeneratorService _documents;
        private WalletService _wallet;
        private IMoneyAggregator _money;
        private IReadOnlyCacheData<OfficeEntries> _officeData;
        private IReadOnlyCacheData<IncomeEntries> _incomeData;
        private GameStatisticsService _statistics;
        private CacheVersionService _cacheVersions;

        private OfficeRestoreData _pendingRestore;
        private int _nextClerkId = 1;
        private int _nextProcessingClerkIndex;
        private int _hireClaimEpoch;
        private long _nextHireClaimToken;
        private long _nextHireRequestId = 1;
        private int _transactionDepth;
        private bool _changePending;
        private bool _initialized;
        private bool _updateSubscribed;
        private bool _isTicking;
        private bool _isMutating;
        private bool _isUpdatingStatistics;

        public string SaveId => "office";
        public bool IsUnlocked => _unlocks?.IsUnlocked(FeatureIds.Office) ?? false;
        public int ClerkCount => _clerks.Count;
        public int PendingHireCount => _pendingHires.Count;
        public int ClerkCapacity => _officeData?.Value.ClerkCapacity ?? 0;
        public IReadOnlyList<OfficeClerkState> Clerks => _readOnlyClerks;
        public Observable<Unit> Changed => _changed;
        public Observable<OfficeDocumentResult> DocumentProcessed => _documentProcessed;

        public OfficeService() : this(null, null) { }

        internal OfficeService(Func<float> randomValue, Observable<float> updateStream) {
            _randomValue = randomValue ?? (() => UnityEngine.Random.value);
            _updateStream = updateStream;
            _readOnlyClerks = _clerks.AsReadOnly();
        }

        public UniTask InitializeAsync(IServiceScope scope) {
            BeginTransaction();
            try {
                _unlocks = scope.Get<UnlockService>();
                _documents = scope.Get<DocumentGeneratorService>();
                _wallet = scope.Get<WalletService>();
                _money = scope.Get<IMoneyAggregator>();
                PlayerStatStash stash = scope.Get<PlayerStatStash>();
                _officeData = stash.OfficeData;
                _incomeData = stash.IncomeData;
                _statistics = scope.Get<GameStatisticsService>();
                _cacheVersions = scope.Get<CacheVersionService>();
                _initialized = true;

                if (_pendingRestore != null) {
                    ApplyRestore(_pendingRestore);
                    _pendingRestore = null;
                }

                _unlocks.Changed.Subscribe(_ => RequestChanged()).AddTo(_subscriptions);
                _wallet.BalanceChanged.Subscribe(_ => RequestChanged()).AddTo(_subscriptions);
                _cacheVersions.Invalidated.Subscribe(OnCacheInvalidated).AddTo(_subscriptions);
                _statistics.Changed.Subscribe(_ => OnStatisticsChanged()).AddTo(_subscriptions);
                ReconcileClerkStatistic();
                RequestChanged();
            } finally {
                EndTransaction();
            }

            return UniTask.CompletedTask;
        }

        public UniTask PostInitializeAsync(IServiceScope scope) {
            if (_updateSubscribed) return UniTask.CompletedTask;
            _updateSubscribed = true;
            Observable<float> stream = _updateStream ?? Observable.EveryUpdate().Select(_ => Time.deltaTime);
            stream.Subscribe(Tick).AddTo(_subscriptions);
            return UniTask.CompletedTask;
        }

        public bool CanStartClerkHire(Value bid) {
            return CanStartClerkHireCore(bid);
        }

        public bool TryStartClerkHire(Value bid) {
            if (!_initialized || _isMutating || _isTicking) return false;

            _isMutating = true;
            BeginTransaction();
            try {
                if (!CanStartClerkHireCore(bid)) return false;

                OfficeEntries entries = _officeData.Value;
                double rolledMultiplier = RollClerkMultiplier(bid, entries);
                double maximumSignatureMultiplier = entries.MaximumHireSignatureMultiplier;
                var pending = new PendingClerkHireRecord(
                    _nextHireRequestId,
                    bid,
                    rolledMultiplier,
                    maximumSignatureMultiplier);

                // The injected random callback is allowed to invoke arbitrary test/application code.
                // Revalidate all external eligibility after it returns and before touching the wallet.
                if (!CanStartClerkHireCore(bid)) return false;

                Value balanceBefore = _wallet.CurrentBalance;
                if (!_wallet.TryWithdrawWallet(bid, false) || _wallet.CurrentBalance == balanceBefore) return false;

                _pendingHires.Add(pending);
                _nextHireRequestId++;
                _wallet.NotifyBalanceChanged();
                RequestChanged();
                return true;
            } finally {
                try {
                    EndTransaction();
                } finally {
                    _isMutating = false;
                }
            }
        }

        internal bool TryClaimPendingClerkHire(out ClerkHireDocumentClaim claim) {
            claim = default;
            if (!_initialized || _isMutating || _isTicking || _nextHireClaimToken == long.MaxValue) return false;

            for (int index = 0; index < _pendingHires.Count; index++) {
                long requestId = _pendingHires[index].RequestId;
                if (_claimedHireRequestIds.Contains(requestId)) continue;

                long token = ++_nextHireClaimToken;
                _activeHireClaims.Add(token, requestId);
                _claimedHireRequestIds.Add(requestId);
                claim = new ClerkHireDocumentClaim(_hireClaimEpoch, token, requestId);
                return true;
            }

            return false;
        }

        internal bool TryCompletePendingClerkHire(
            ClerkHireDocumentClaim claim,
            SignatureEvaluationResult result) {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (!_initialized || _isMutating || _isTicking || !IsValidHireClaim(claim)) return false;

            int pendingIndex = FindPendingHireIndex(claim.RequestId);
            if (pendingIndex < 0) {
                ReleaseHireClaim(claim);
                return false;
            }

            PendingClerkHireRecord pending = _pendingHires[pendingIndex];
            bool accepted = result.Status == SignatureEvaluationStatus.Accepted;
            double finalMultiplier = accepted
                ? SaturatingMultiply(pending.RolledBaseMultiplier,
                    ResolveSignatureMultiplier(result, pending.MaximumSignatureMultiplier))
                : 0d;

            _isMutating = true;
            BeginTransaction();
            try {
                ReleaseHireClaim(claim);
                _pendingHires.RemoveAt(pendingIndex);

                if (accepted) {
                    _clerks.Add(new OfficeClerkState(_nextClerkId, finalMultiplier));
                    _nextClerkId++;
                    ReconcileClerkStatistic();
                }
                else {
                    _wallet.ReplenishWallet(pending.PaidPrice);
                }

                RequestChanged();
                return true;
            } finally {
                try {
                    EndTransaction();
                } finally {
                    _isMutating = false;
                }
            }
        }

        internal bool TryReleasePendingClerkHire(ClerkHireDocumentClaim claim) {
            if (!IsValidHireClaim(claim)) return false;
            ReleaseHireClaim(claim);
            return true;
        }

        public void Tick(float deltaTime) {
            if (!_initialized || _isMutating || _isTicking || float.IsNaN(deltaTime) ||
                float.IsInfinity(deltaTime) || deltaTime <= 0f) {
                return;
            }

            BeginTransaction();
            _isTicking = true;
            try {
                int clerkCount = _clerks.Count;
                if (clerkCount == 0 || !IsUnlocked) return;

                OfficeEntries entries = _officeData.Value;
                float speed = entries.DocumentsPerSecondPerClerk;
                if (speed <= 0f) return;

                Value incomePerDocument = _incomeData.Value.IncomePerDocument;
                for (int index = 0; index < clerkCount; index++) {
                    double work = _clerks[index].Progress + (double)deltaTime * speed;
                    _work[index] = double.IsNaN(work) || double.IsInfinity(work)
                        ? MaxDocumentsProcessedPerTick + 1d
                        : Math.Max(0d, work);
                }

                int budget = MaxDocumentsProcessedPerTick;
                int processed = 0;
                int accepted = 0;
                int rejected = 0;
                bool documentsAvailable = true;

                while (budget > 0 && documentsAvailable) {
                    bool anyReady = false;
                    int roundStart = _nextProcessingClerkIndex;
                    for (int offset = 0; offset < clerkCount && budget > 0; offset++) {
                        int index = (roundStart + offset) % clerkCount;
                        if (_work[index] < 1d) continue;
                        anyReady = true;
                        if (!_documents.TryObtainDocument()) {
                            documentsAvailable = false;
                            break;
                        }

                        _work[index] -= 1d;
                        bool wasAccepted = ProcessDocument(_clerks[index], entries, incomePerDocument);
                        processed++;
                        if (wasAccepted) accepted++;
                        else rejected++;
                        budget--;
                        _nextProcessingClerkIndex = (index + 1) % clerkCount;
                    }

                    if (!anyReady) break;
                }

                bool progressChanged = processed > 0;
                for (int index = 0; index < clerkCount; index++) {
                    float nextProgress = _work[index] >= 1d
                        ? 1f
                        : (float)Math.Clamp(_work[index], 0d, 1d);
                    if (_clerks[index].Progress.Equals(nextProgress)) continue;
                    _clerks[index].Progress = nextProgress;
                    progressChanged = true;
                }

                if (processed > 0) ApplyProcessingStatistics(processed, accepted, rejected);
                if (progressChanged) RequestChanged();
            } finally {
                try {
                    EndTransaction();
                } finally {
                    _isTicking = false;
                }
            }
        }

        public JToken Serialize() {
            var clerks = new JArray();
            for (int index = 0; index < _clerks.Count; index++) {
                OfficeClerkState clerk = _clerks[index];
                clerks.Add(new JObject {
                    ["id"] = clerk.Id,
                    ["progress"] = clerk.Progress,
                    ["incomeMultiplier"] = clerk.IncomeMultiplier
                });
            }

            var pendingHires = new JArray();
            for (int index = 0; index < _pendingHires.Count; index++) {
                PendingClerkHireRecord pending = _pendingHires[index];
                pendingHires.Add(new JObject {
                    ["requestId"] = pending.RequestId,
                    ["paidStored"] = pending.PaidPrice.Stored,
                    ["paidDegree"] = pending.PaidPrice.Base.Degree,
                    ["rolledBaseMultiplier"] = pending.RolledBaseMultiplier,
                    ["maximumSignatureMultiplier"] = pending.MaximumSignatureMultiplier
                });
            }

            return new JObject {
                ["nextClerkId"] = _nextClerkId,
                ["nextProcessingClerkIndex"] = _nextProcessingClerkIndex,
                ["nextHireRequestId"] = _nextHireRequestId,
                ["clerks"] = clerks,
                ["pendingHires"] = pendingHires
            };
        }

        public void Deserialize(JToken state) {
            if (_isTicking || _isMutating) {
                throw new InvalidOperationException("Office state cannot be restored during another office mutation.");
            }

            OfficeRestoreData restored;
            try {
                restored = ParseRestore(state);
            } catch {
                if (_initialized) {
                    BeginTransaction();
                    try {
                        ReconcileClerkStatistic();
                    } finally {
                        EndTransaction();
                    }
                }

                throw;
            }

            if (!_initialized) {
                _pendingRestore = restored;
                return;
            }

            _isMutating = true;
            BeginTransaction();
            try {
                ApplyRestore(restored);
                ReconcileClerkStatistic();
                RequestChanged();
            } finally {
                try {
                    EndTransaction();
                } finally {
                    _isMutating = false;
                }
            }
        }

        public void Dispose() {
            _subscriptions.Dispose();
            _documentProcessed.Dispose();
            _changed.Dispose();
            InvalidateHireClaims();
            _clerks.Clear();
            _pendingHires.Clear();
            _pendingRestore = null;
            _initialized = false;
        }

        private bool CanStartClerkHireCore(Value bid) {
            if (!_initialized || !IsUnlocked || !IsCanonicalPositiveBid(bid) ||
                (long)ClerkCount + PendingHireCount >= ClerkCapacity ||
                (long)_nextClerkId + PendingHireCount + 1L > int.MaxValue ||
                _nextHireRequestId == long.MaxValue) {
                return false;
            }

            Value balance = _wallet.CurrentBalance;
            if (!balance.IsSignificant(bid) || !_wallet.CanAfford(bid)) return false;
            Value? balanceAfterDebit = balance - bid;
            return balanceAfterDebit.HasValue && balanceAfterDebit.Value != balance;
        }

        private double RollClerkMultiplier(Value bid, OfficeEntries entries) {
            double bidLogarithm = Math.Max(0d, bid.ToLog10());
            double median = SaturatingAdd(entries.BaseClerkMultiplierMedian, bidLogarithm);
            double lower = median - entries.ClerkMultiplierRangeStep;
            double upper = SaturatingAdd(median, entries.ClerkMultiplierRangeStep);

            float random = _randomValue();
            if (float.IsNaN(random) || float.IsInfinity(random)) random = 0f;
            double unit = Math.Clamp((double)random, 0d, 1d);
            double rolled = unit <= 0d
                ? lower
                : unit >= 1d
                    ? upper
                    : (1d - unit) * lower + unit * upper;
            if (double.IsPositiveInfinity(rolled)) rolled = double.MaxValue;
            if (double.IsNegativeInfinity(rolled) || double.IsNaN(rolled)) rolled = 0d;
            return Math.Max(entries.MinimumClerkMultiplier, rolled);
        }

        private bool ProcessDocument(OfficeClerkState clerk, OfficeEntries entries, Value incomePerDocument) {
            float roll = _randomValue();
            if (float.IsNaN(roll) || float.IsInfinity(roll)) roll = 0f;
            roll = Math.Clamp(roll, 0f, 1f);
            float quality = roll * entries.QualityCeiling;
            bool accepted = quality >= entries.AcceptanceThreshold;
            double rewardFactor = accepted
                ? SaturatingMultiply(quality * entries.RewardMultiplier, clerk.IncomeMultiplier)
                : 0d;
            Value requested = accepted ? MultiplyValueSafely(incomePerDocument, rewardFactor) : Value.Zero;
            Value credited = accepted ? _money.AddMoney(requested) : Value.Zero;
            _documentProcessed.OnNext(new OfficeDocumentResult(
                clerk.Id,
                accepted,
                quality,
                requested,
                credited));
            return accepted;
        }

        private void ApplyProcessingStatistics(int processed, int accepted, int rejected) {
            int count = 0;
            _tickStatisticMutations[count++] = GameStatisticMutation.Add(
                GameStatisticIds.OfficeProcessedDocuments, processed);
            if (accepted > 0) {
                _tickStatisticMutations[count++] = GameStatisticMutation.Add(
                    GameStatisticIds.OfficeAcceptedDocuments, accepted);
            }
            if (rejected > 0) {
                _tickStatisticMutations[count++] = GameStatisticMutation.Add(
                    GameStatisticIds.OfficeRejectedDocuments, rejected);
            }

            ApplyStatisticsBatch(_tickStatisticMutations, count);
        }

        private void ReconcileClerkStatistic() {
            if (!_initialized) return;
            if (_statistics.TryGetValue(GameStatisticIds.OfficeClerkCount, out double current) &&
                current.Equals((double)ClerkCount)) {
                return;
            }

            _singleStatisticMutation[0] = GameStatisticMutation.Set(GameStatisticIds.OfficeClerkCount, ClerkCount);
            if (ApplyStatisticsBatch(_singleStatisticMutation, 1)) RequestChanged();
        }

        private bool ApplyStatisticsBatch(IReadOnlyList<GameStatisticMutation> mutations, int count) {
            _isUpdatingStatistics = true;
            try {
                return _statistics.ApplyBatch(mutations, count);
            } finally {
                _isUpdatingStatistics = false;
            }
        }

        private void OnStatisticsChanged() {
            if (_isUpdatingStatistics || !_initialized) return;
            BeginTransaction();
            try {
                ReconcileClerkStatistic();
            } finally {
                EndTransaction();
            }
        }

        private void OnCacheInvalidated(Type type) {
            if (type == typeof(OfficeEntries)) RequestChanged();
        }

        private bool IsValidHireClaim(ClerkHireDocumentClaim claim) {
            return claim.Epoch == _hireClaimEpoch &&
                   _activeHireClaims.TryGetValue(claim.Token, out long requestId) &&
                   requestId == claim.RequestId;
        }

        private void ReleaseHireClaim(ClerkHireDocumentClaim claim) {
            _activeHireClaims.Remove(claim.Token);
            _claimedHireRequestIds.Remove(claim.RequestId);
        }

        private int FindPendingHireIndex(long requestId) {
            for (int index = 0; index < _pendingHires.Count; index++) {
                if (_pendingHires[index].RequestId == requestId) return index;
            }

            return -1;
        }

        private void InvalidateHireClaims() {
            _hireClaimEpoch++;
            _activeHireClaims.Clear();
            _claimedHireRequestIds.Clear();
        }

        private void ApplyRestore(OfficeRestoreData restored) {
            InvalidateHireClaims();
            _clerks.Clear();
            for (int index = 0; index < restored.Clerks.Count; index++) {
                RestoredClerk clerk = restored.Clerks[index];
                _clerks.Add(new OfficeClerkState(clerk.Id, clerk.IncomeMultiplier, clerk.Progress));
            }

            _pendingHires.Clear();
            _pendingHires.AddRange(restored.PendingHires);
            _nextClerkId = restored.NextClerkId;
            _nextProcessingClerkIndex = restored.NextProcessingClerkIndex;
            _nextHireRequestId = restored.NextHireRequestId;
        }

        private static OfficeRestoreData ParseRestore(JToken state) {
            if (state is not JObject root || root["nextClerkId"]?.Type != JTokenType.Integer ||
                root["clerks"] is not JArray clerksToken) {
                throw new JsonSerializationException(
                    "Office state must contain an integer nextClerkId and a clerks array.");
            }

            bool hasNextHireRequestId = root["nextHireRequestId"] != null;
            bool hasPendingHires = root["pendingHires"] != null;
            if (hasPendingHires && !hasNextHireRequestId) {
                throw new JsonSerializationException(
                    "Office pending hire state requires a nextHireRequestId counter.");
            }

            if (hasPendingHires && root["pendingHires"] is not JArray) {
                throw new JsonSerializationException("Office pending hire state must be an array.");
            }

            if (clerksToken.Count > OfficeCacheCalculator.MaximumClerkCapacity) {
                throw new JsonSerializationException(
                    $"Office state cannot contain more than {OfficeCacheCalculator.MaximumClerkCapacity} clerks.");
            }

            int nextClerkId = root["nextClerkId"].Value<int>();
            if (nextClerkId <= 0) {
                throw new JsonSerializationException("Office next clerk ID must be positive.");
            }

            var clerkIds = new HashSet<int>();
            var clerks = new List<RestoredClerk>(clerksToken.Count);
            int maximumClerkId = 0;
            for (int index = 0; index < clerksToken.Count; index++) {
                if (clerksToken[index] is not JObject data || data["id"]?.Type != JTokenType.Integer ||
                    !TryReadNumber(data["progress"], out float progress)) {
                    throw new JsonSerializationException(
                        $"Office clerk at index {index} must contain an integer ID and numeric progress.");
                }

                double incomeMultiplier = 1d;
                if (data["incomeMultiplier"] != null &&
                    !TryReadNumber(data["incomeMultiplier"], out incomeMultiplier)) {
                    throw new JsonSerializationException(
                        $"Office clerk at index {index} has a non-numeric income multiplier.");
                }

                int id = data["id"].Value<int>();
                if (id <= 0 || !clerkIds.Add(id) || !IsFiniteNonNegative(incomeMultiplier) ||
                    float.IsNaN(progress) || float.IsInfinity(progress) || progress < 0f || progress > 1f) {
                    throw new JsonSerializationException(
                        $"Office clerk at index {index} has an invalid ID, progress, or income multiplier.");
                }

                maximumClerkId = Math.Max(maximumClerkId, id);
                clerks.Add(new RestoredClerk(id, progress, incomeMultiplier));
            }

            if (nextClerkId <= maximumClerkId) {
                throw new JsonSerializationException("Office next clerk ID must be greater than every restored clerk ID.");
            }

            int nextProcessingClerkIndex = 0;
            JToken cursorToken = root["nextProcessingClerkIndex"];
            if (cursorToken != null) {
                if (cursorToken.Type != JTokenType.Integer) {
                    throw new JsonSerializationException("Office processing clerk index must be an integer.");
                }

                nextProcessingClerkIndex = cursorToken.Value<int>();
            }

            int maximumCursor = clerks.Count == 0 ? 0 : clerks.Count - 1;
            if (nextProcessingClerkIndex < 0 || nextProcessingClerkIndex > maximumCursor) {
                throw new JsonSerializationException("Office processing clerk index is outside the clerk list.");
            }

            long nextHireRequestId = 1;
            if (hasNextHireRequestId) {
                if (root["nextHireRequestId"].Type != JTokenType.Integer ||
                    !TryReadInt64(root["nextHireRequestId"], out nextHireRequestId) || nextHireRequestId <= 0) {
                    throw new JsonSerializationException("Office next hire request ID must be a positive integer.");
                }
            }

            var pendingHires = new List<PendingClerkHireRecord>();
            var requestIds = new HashSet<long>();
            long maximumRequestId = 0;
            if (root["pendingHires"] is JArray pendingToken) {
                pendingHires.Capacity = pendingToken.Count;
                for (int index = 0; index < pendingToken.Count; index++) {
                    if (pendingToken[index] is not JObject data ||
                        !TryReadInt64(data["requestId"], out long requestId) ||
                        !TryReadValue(data["paidStored"], data["paidDegree"], out Value paidPrice) ||
                        !TryReadNumber(data["rolledBaseMultiplier"], out double rolledBaseMultiplier) ||
                        !TryReadNumber(data["maximumSignatureMultiplier"], out double maximumSignatureMultiplier)) {
                        throw new JsonSerializationException(
                            $"Office pending hire at index {index} is missing required canonical values.");
                    }

                    if (requestId <= 0 || !requestIds.Add(requestId) ||
                        !IsFiniteNonNegative(rolledBaseMultiplier) ||
                        !IsFiniteAtLeastOne(maximumSignatureMultiplier)) {
                        throw new JsonSerializationException(
                            $"Office pending hire at index {index} has values outside valid ranges.");
                    }

                    maximumRequestId = Math.Max(maximumRequestId, requestId);
                    pendingHires.Add(new PendingClerkHireRecord(
                        requestId,
                        paidPrice,
                        rolledBaseMultiplier,
                        maximumSignatureMultiplier));
                }
            }

            if (nextHireRequestId <= maximumRequestId) {
                throw new JsonSerializationException(
                    "Office next hire request ID must be greater than every restored request ID.");
            }

            if (clerks.Count + pendingHires.Count > OfficeCacheCalculator.MaximumClerkCapacity) {
                throw new JsonSerializationException(
                    $"Office state cannot contain more than {OfficeCacheCalculator.MaximumClerkCapacity} clerks and pending hires combined.");
            }

            if ((long)nextClerkId + pendingHires.Count > int.MaxValue) {
                throw new JsonSerializationException(
                    "Office state does not have enough clerk ID headroom for its pending hires.");
            }

            return new OfficeRestoreData(
                nextClerkId,
                nextProcessingClerkIndex,
                nextHireRequestId,
                clerks,
                pendingHires);
        }

        private static double ResolveSignatureMultiplier(
            SignatureEvaluationResult result,
            double maximumSignatureMultiplier) {
            float similarity = result.Similarity;
            float minimum = result.MinimumSimilarity;
            bool malformed = float.IsNaN(similarity) || float.IsInfinity(similarity) ||
                             float.IsNaN(minimum) || float.IsInfinity(minimum) ||
                             similarity < 0f || similarity > 1f || minimum < 0f || minimum > 1f ||
                             similarity < minimum;
            if (malformed) return 1d;
            if (minimum >= 1f) return maximumSignatureMultiplier;

            double quality = Math.Clamp((similarity - minimum) / (1d - minimum), 0d, 1d);
            return 1d + (maximumSignatureMultiplier - 1d) * quality;
        }

        private static Value MultiplyValueSafely(Value value, double multiplier) {
            if (value.IsZero || multiplier <= 0d) return Value.Zero;
            if (value.Base.Degree == int.MaxValue) return Value.Infinity;

            double resultLog10 = value.ToLog10() + Math.Log10(multiplier);
            if (double.IsNaN(resultLog10)) return Value.Zero;
            if (double.IsPositiveInfinity(resultLog10) || resultLog10 >= MaximumValueLog10) {
                return Value.Infinity;
            }

            return Value.FromLog10(resultLog10);
        }

        private static double SaturatingAdd(double left, double right) {
            if (left >= double.MaxValue - right) return double.MaxValue;
            return left + right;
        }

        private static double SaturatingMultiply(double left, double right) {
            if (left <= 0d || right <= 0d) return 0d;
            if (left >= double.MaxValue / right) return double.MaxValue;
            return left * right;
        }

        private static bool IsCanonicalPositiveBid(Value value) {
            double stored = value.Stored;
            int degree = value.Base.Degree;
            if (double.IsNaN(stored) || double.IsInfinity(stored) || stored <= 0d || stored >= 1000d ||
                degree < 0 || degree == int.MaxValue || degree > 0 && stored < 1d) {
                return false;
            }

            var canonical = new Value(stored, new BaseValue(degree));
            return canonical.Stored.Equals(stored) && canonical.Base.Degree == degree;
        }

        private static bool TryReadValue(JToken storedToken, JToken degreeToken, out Value value) {
            value = default;
            if (!TryReadNumber(storedToken, out double stored) || degreeToken?.Type != JTokenType.Integer) {
                return false;
            }

            int degree = degreeToken.Value<int>();
            if (double.IsNaN(stored) || double.IsInfinity(stored) || stored <= 0d || stored >= 1000d ||
                degree < 0 || degree == int.MaxValue || degree > 0 && stored < 1d) {
                return false;
            }

            var candidate = new Value(stored, new BaseValue(degree));
            if (!candidate.Stored.Equals(stored) || candidate.Base.Degree != degree) return false;
            value = candidate;
            return true;
        }

        private static bool TryReadNumber(JToken token, out float value) {
            if (token?.Type is JTokenType.Integer or JTokenType.Float) {
                value = token.Value<float>();
                return true;
            }

            value = default;
            return false;
        }

        private static bool TryReadNumber(JToken token, out double value) {
            if (token?.Type is JTokenType.Integer or JTokenType.Float) {
                value = token.Value<double>();
                return true;
            }

            value = default;
            return false;
        }

        private static bool TryReadInt64(JToken token, out long value) {
            value = default;
            if (token?.Type != JTokenType.Integer) return false;
            try {
                value = token.Value<long>();
                return true;
            } catch (OverflowException) {
                return false;
            }
        }

        private static bool IsFiniteNonNegative(double value) {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
        }

        private static bool IsFiniteAtLeastOne(double value) {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 1d;
        }

        private void BeginTransaction() {
            _transactionDepth++;
        }

        private void EndTransaction() {
            _transactionDepth--;
            if (_transactionDepth != 0 || !_changePending) return;
            _changePending = false;
            _changed.OnNext(Unit.Default);
        }

        private void RequestChanged() {
            if (_transactionDepth > 0) {
                _changePending = true;
                return;
            }

            _changed.OnNext(Unit.Default);
        }

        private sealed class OfficeRestoreData {
            public int NextClerkId { get; }
            public int NextProcessingClerkIndex { get; }
            public long NextHireRequestId { get; }
            public List<RestoredClerk> Clerks { get; }
            public List<PendingClerkHireRecord> PendingHires { get; }

            public OfficeRestoreData(
                int nextClerkId,
                int nextProcessingClerkIndex,
                long nextHireRequestId,
                List<RestoredClerk> clerks,
                List<PendingClerkHireRecord> pendingHires) {
                NextClerkId = nextClerkId;
                NextProcessingClerkIndex = nextProcessingClerkIndex;
                NextHireRequestId = nextHireRequestId;
                Clerks = clerks;
                PendingHires = pendingHires;
            }
        }

        private readonly struct RestoredClerk {
            public int Id { get; }
            public float Progress { get; }
            public double IncomeMultiplier { get; }

            public RestoredClerk(int id, float progress, double incomeMultiplier) {
                Id = id;
                Progress = progress;
                IncomeMultiplier = incomeMultiplier;
            }
        }

        private readonly struct PendingClerkHireRecord {
            public long RequestId { get; }
            public Value PaidPrice { get; }
            public double RolledBaseMultiplier { get; }
            public double MaximumSignatureMultiplier { get; }

            public PendingClerkHireRecord(
                long requestId,
                Value paidPrice,
                double rolledBaseMultiplier,
                double maximumSignatureMultiplier) {
                RequestId = requestId;
                PaidPrice = paidPrice;
                RolledBaseMultiplier = rolledBaseMultiplier;
                MaximumSignatureMultiplier = maximumSignatureMultiplier;
            }
        }

        internal readonly struct ClerkHireDocumentClaim {
            internal int Epoch { get; }
            internal long Token { get; }
            internal long RequestId { get; }

            internal ClerkHireDocumentClaim(int epoch, long token, long requestId) {
                Epoch = epoch;
                Token = token;
                RequestId = requestId;
            }
        }
    }
}
