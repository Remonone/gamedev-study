using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Office;
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

        private readonly List<OfficeClerkState> _clerks = new();
        private readonly ReadOnlyCollection<OfficeClerkState> _readOnlyClerks;
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
        private int _transactionDepth;
        private bool _changePending;
        private bool _initialized;
        private bool _updateSubscribed;
        private bool _isTicking;
        private bool _isUpdatingStatistics;

        public string SaveId => "office";
        public bool IsUnlocked => _unlocks?.IsUnlocked(FeatureIds.Office) ?? false;
        public int ClerkCount => _clerks.Count;
        public int ClerkCapacity => _officeData?.Value.ClerkCapacity ?? 0;
        public IReadOnlyList<OfficeClerkState> Clerks => _readOnlyClerks;
        public Observable<Unit> Changed => _changed;
        public Observable<OfficeDocumentResult> DocumentProcessed => _documentProcessed;

        public bool CanHireClerk {
            get {
                if (!_initialized || !IsUnlocked || ClerkCount >= ClerkCapacity || _nextClerkId == int.MaxValue) {
                    return false;
                }

                Value cost = ResolveNextHireCost();
                return !IsInfinite(cost) && _wallet.CanAfford(cost);
            }
        }

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
            Observable<float> stream = _updateStream ??
                                       Observable.EveryUpdate().Select(_ => Time.deltaTime);
            stream.Subscribe(Tick).AddTo(_subscriptions);
            return UniTask.CompletedTask;
        }

        public Value ResolveNextHireCost() {
            if (!_initialized) return Value.Infinity;
            OfficeEntries entries = _officeData.Value;
            double baseLog10 = entries.BaseHireCost.ToLog10();
            double growthLog10 = Math.Log10(entries.HireCostGrowthMultiplier);
            double resultLog10 = baseLog10 + growthLog10 * ClerkCount;
            double maximumLog10 = int.MaxValue * 3d;
            if (double.IsNaN(resultLog10) || double.IsInfinity(resultLog10) || resultLog10 >= maximumLog10) {
                return Value.Infinity;
            }

            return Value.FromLog10(resultLog10);
        }

        public bool TryHireClerk() {
            if (!_initialized) return false;

            BeginTransaction();
            try {
                if (!IsUnlocked || ClerkCount >= ClerkCapacity || _nextClerkId == int.MaxValue) return false;

                Value cost = ResolveNextHireCost();
                if (IsInfinite(cost) || !_wallet.TryWithdrawWallet(cost)) return false;

                _clerks.Add(new OfficeClerkState(_nextClerkId));
                _nextClerkId++;
                ReconcileClerkStatistic();
                RequestChanged();
                return true;
            } finally {
                EndTransaction();
            }
        }

        public void Tick(float deltaTime) {
            if (!_initialized || _isTicking || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) ||
                deltaTime <= 0f) {
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
                        bool wasAccepted = ProcessDocument(_clerks[index].Id, entries, incomePerDocument);
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
                    ["progress"] = clerk.Progress
                });
            }

            return new JObject {
                ["nextClerkId"] = _nextClerkId,
                ["nextProcessingClerkIndex"] = _nextProcessingClerkIndex,
                ["clerks"] = clerks
            };
        }

        public void Deserialize(JToken state) {
            if (_isTicking) {
                throw new InvalidOperationException("Office state cannot be restored during a processing tick.");
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

            BeginTransaction();
            try {
                ApplyRestore(restored);
                ReconcileClerkStatistic();
                RequestChanged();
            } finally {
                EndTransaction();
            }
        }

        public void Dispose() {
            _subscriptions.Dispose();
            _documentProcessed.Dispose();
            _changed.Dispose();
            _clerks.Clear();
            _pendingRestore = null;
            _initialized = false;
        }

        private bool ProcessDocument(int clerkId, OfficeEntries entries, Value incomePerDocument) {
            float roll = _randomValue();
            if (float.IsNaN(roll) || float.IsInfinity(roll)) roll = 0f;
            roll = Math.Clamp(roll, 0f, 1f);
            float quality = roll * entries.QualityCeiling;
            bool accepted = quality >= entries.AcceptanceThreshold;
            Value requested = accepted
                ? incomePerDocument * (quality * entries.RewardMultiplier)
                : Value.Zero;
            Value credited = accepted ? _money.AddMoney(requested) : Value.Zero;
            _documentProcessed.OnNext(new OfficeDocumentResult(clerkId, accepted, quality, requested, credited));
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

        private void ApplyRestore(OfficeRestoreData restored) {
            _clerks.Clear();
            for (int index = 0; index < restored.Clerks.Count; index++) {
                RestoredClerk clerk = restored.Clerks[index];
                _clerks.Add(new OfficeClerkState(clerk.Id, clerk.Progress));
            }

            _nextClerkId = restored.NextClerkId;
            _nextProcessingClerkIndex = restored.NextProcessingClerkIndex;
        }

        private static OfficeRestoreData ParseRestore(JToken state) {
            if (state is not JObject root || root["nextClerkId"]?.Type != JTokenType.Integer ||
                root["clerks"] is not JArray clerksToken) {
                throw new JsonSerializationException(
                    "Office state must contain an integer nextClerkId and a clerks array.");
            }

            if (clerksToken.Count > OfficeCacheCalculator.MaximumClerkCapacity) {
                throw new JsonSerializationException(
                    $"Office state cannot contain more than {OfficeCacheCalculator.MaximumClerkCapacity} clerks.");
            }

            int nextClerkId = root["nextClerkId"].Value<int>();
            if (nextClerkId <= 0) {
                throw new JsonSerializationException("Office next clerk ID must be positive.");
            }

            var ids = new HashSet<int>();
            var clerks = new List<RestoredClerk>(clerksToken.Count);
            int maximumId = 0;
            for (int index = 0; index < clerksToken.Count; index++) {
                if (clerksToken[index] is not JObject data || data["id"]?.Type != JTokenType.Integer ||
                    !TryReadNumber(data["progress"], out float progress)) {
                    throw new JsonSerializationException(
                        $"Office clerk at index {index} must contain an integer ID and numeric progress.");
                }

                int id = data["id"].Value<int>();
                if (id <= 0 || !ids.Add(id) || float.IsNaN(progress) || float.IsInfinity(progress) ||
                    progress < 0f || progress > 1f) {
                    throw new JsonSerializationException(
                        $"Office clerk at index {index} has an invalid ID or progress.");
                }

                maximumId = Math.Max(maximumId, id);
                clerks.Add(new RestoredClerk(id, progress));
            }

            if (nextClerkId <= maximumId) {
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

            return new OfficeRestoreData(nextClerkId, nextProcessingClerkIndex, clerks);
        }

        private static bool TryReadNumber(JToken token, out float value) {
            if (token?.Type is JTokenType.Integer or JTokenType.Float) {
                value = token.Value<float>();
                return true;
            }

            value = default;
            return false;
        }

        private static bool IsInfinite(Value value) {
            return value.Base.Degree == int.MaxValue;
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
            public List<RestoredClerk> Clerks { get; }

            public OfficeRestoreData(int nextClerkId, int nextProcessingClerkIndex, List<RestoredClerk> clerks) {
                NextClerkId = nextClerkId;
                NextProcessingClerkIndex = nextProcessingClerkIndex;
                Clerks = clerks;
            }
        }

        private readonly struct RestoredClerk {
            public int Id { get; }
            public float Progress { get; }

            public RestoredClerk(int id, float progress) {
                Id = id;
                Progress = progress;
            }
        }
    }
}
