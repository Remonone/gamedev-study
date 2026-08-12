using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Documents;
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
        private const int MinimumClerkAge = 18;
        private const int MaximumClerkAge = 65;
        private const int MaximumClerkNameLength = 64;

        private static readonly string[] ClerkNames = {
            "Alex", "Bailey", "Casey", "Dana", "Elliot", "Harper", "Jamie", "Jordan",
            "Morgan", "Parker", "Quinn", "Reese", "Robin", "Rory", "Taylor", "Avery"
        };

        private readonly List<OfficeClerkState> _clerks = new();
        private readonly ReadOnlyCollection<OfficeClerkState> _readOnlyClerks;
        private readonly List<PendingClerkHireRecord> _pendingHires = new();
        private readonly Dictionary<long, long> _activeHireClaims = new();
        private readonly HashSet<long> _claimedHireRequestIds = new();
        private readonly List<PendingSalaryReviewRecord> _pendingSalaryReviews = new();
        private readonly Dictionary<long, long> _activeSalaryReviewClaims = new();
        private readonly HashSet<long> _claimedSalaryReviewRequestIds = new();
        private readonly double[] _work = new double[OfficeCacheCalculator.MaximumClerkCapacity];
        private readonly GameStatisticMutation[] _tickStatisticMutations = new GameStatisticMutation[3];
        private readonly GameStatisticMutation[] _singleStatisticMutation = new GameStatisticMutation[1];
        private readonly Subject<Unit> _changed = new();
        private readonly Subject<Unit> _documentOffersChanged = new();
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
        private IReadOnlyCacheData<DocumentEntries> _documentData;
        private AcceptedNormalDocumentService _acceptedDocuments;
        private GameStatisticsService _statistics;
        private CacheVersionService _cacheVersions;

        private OfficeRestoreData _pendingRestore;
        private int _nextClerkId = 1;
        private int _nextProcessingClerkIndex;
        private int _hireClaimEpoch;
        private long _nextHireClaimToken;
        private long _nextHireRequestId = 1;
        private int _salaryReviewClaimEpoch;
        private long _nextSalaryReviewClaimToken;
        private long _nextSalaryReviewRequestId = 1;
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
        public int PendingSalaryReviewCount => _pendingSalaryReviews.Count;
        public int ClerkCapacity => _officeData?.Value.ClerkCapacity ?? 0;
        public float DocumentsPerSecondPerClerk => _officeData?.Value.DocumentsPerSecondPerClerk ?? 0f;
        public float QualityCeiling => _officeData?.Value.QualityCeiling ?? 0f;
        public float AcceptanceThreshold => _officeData?.Value.AcceptanceThreshold ?? 0f;
        public float RewardMultiplier => _officeData?.Value.RewardMultiplier ?? 0f;
        public double SalaryReviewCostRatio => _officeData?.Value.SalaryReviewCostRatio ?? 0d;
        public IReadOnlyList<OfficeClerkState> Clerks => _readOnlyClerks;
        public Observable<Unit> Changed => _changed;
        public Observable<Unit> DocumentOffersChanged => _documentOffersChanged;
        public Observable<OfficeDocumentResult> DocumentProcessed => _documentProcessed;

        public OfficeService() : this(null, null) { }

        internal OfficeService(Func<float> randomValue, Observable<float> updateStream) {
            _randomValue = randomValue ?? (() => UnityEngine.Random.value);
            _updateStream = updateStream;
            _readOnlyClerks = _clerks.AsReadOnly();
        }

        public UniTask InitializeAsync(IServiceScope scope) {
            bool restoredDocumentOffers = false;
            BeginTransaction();
            try {
                _unlocks = scope.Get<UnlockService>();
                _documents = scope.Get<DocumentGeneratorService>();
                _wallet = scope.Get<WalletService>();
                _money = scope.Get<IMoneyAggregator>();
                PlayerStatStash stash = scope.Get<PlayerStatStash>();
                _officeData = stash.OfficeData;
                _incomeData = stash.IncomeData;
                _documentData = stash.Documents;
                scope.TryGet(out _acceptedDocuments);
                _statistics = scope.Get<GameStatisticsService>();
                _cacheVersions = scope.Get<CacheVersionService>();
                _initialized = true;

                if (_pendingRestore != null) {
                    ApplyRestore(_pendingRestore);
                    _pendingRestore = null;
                    restoredDocumentOffers = true;
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

            if (restoredDocumentOffers) NotifyDocumentOffersChanged();

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
            bool documentOffersChanged = false;
            BeginTransaction();
            try {
                if (!CanStartClerkHireCore(bid)) return false;

                OfficeEntries entries = _officeData.Value;
                double rolledMultiplier = RollClerkMultiplier(bid, entries);
                string clerkName = RollClerkName();
                int clerkAge = RollClerkAge();
                double maximumSignatureMultiplier = entries.MaximumHireSignatureMultiplier;
                var pending = new PendingClerkHireRecord(
                    _nextHireRequestId,
                    bid,
                    rolledMultiplier,
                    maximumSignatureMultiplier,
                    clerkName,
                    clerkAge);

                // The injected random callback is allowed to invoke arbitrary test/application code.
                // Revalidate all external eligibility after it returns and before touching the wallet.
                if (!CanStartClerkHireCore(bid)) return false;

                Value balanceBefore = _wallet.CurrentBalance;
                if (!_wallet.TryWithdrawWallet(bid, false) || _wallet.CurrentBalance == balanceBefore) return false;

                _pendingHires.Add(pending);
                _nextHireRequestId++;
                _wallet.NotifyBalanceChanged();
                RequestChanged();
                documentOffersChanged = true;
                return true;
            } finally {
                try {
                    EndTransaction();
                } finally {
                    _isMutating = false;
                    if (documentOffersChanged) NotifyDocumentOffersChanged();
                }
            }
        }

        internal bool TryClaimPendingClerkHire(out ClerkHireDocumentClaim claim) {
            claim = default;
            return TryPeekPendingClerkHireDocument(out DocumentOffer offer) &&
                   long.TryParse(offer.Key.DomainId, out long requestId) &&
                   TryClaimPendingClerkHire(requestId, out claim);
        }

        internal bool TryPeekPendingClerkHireDocument(out DocumentOffer offer) {
            offer = null;
            if (!_initialized || _isMutating || _isTicking) return false;

            for (int index = 0; index < _pendingHires.Count; index++) {
                PendingClerkHireRecord pending = _pendingHires[index];
                long requestId = pending.RequestId;
                if (_claimedHireRequestIds.Contains(requestId)) continue;

                offer = new DocumentOffer(
                    new DocumentOfferKey(DocumentKind.ClerkHire, requestId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    true,
                    personName: pending.ClerkName,
                    personAge: pending.ClerkAge,
                    amount: pending.PaidPrice,
                    internalMultiplier: pending.RolledBaseMultiplier);
                return true;
            }

            return false;
        }

        internal bool TryClaimPendingClerkHire(long requestedId, out ClerkHireDocumentClaim claim) {
            claim = default;
            if (!_initialized || _isMutating || _isTicking || _nextHireClaimToken == long.MaxValue) return false;

            for (int index = 0; index < _pendingHires.Count; index++) {
                long requestId = _pendingHires[index].RequestId;
                if (requestId != requestedId || _claimedHireRequestIds.Contains(requestId)) continue;

                long token = ++_nextHireClaimToken;
                _activeHireClaims.Add(token, requestId);
                _claimedHireRequestIds.Add(requestId);
                claim = new ClerkHireDocumentClaim(_hireClaimEpoch, token, requestId);
                NotifyDocumentOffersChanged();
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
                NotifyDocumentOffersChanged();
                return false;
            }

            PendingClerkHireRecord pending = _pendingHires[pendingIndex];
            bool accepted = result.Status == SignatureEvaluationStatus.Accepted;
            double bonusEfficiency = accepted
                ? ResolveSignatureMultiplier(result, pending.MaximumSignatureMultiplier) - 1d
                : 0d;

            _isMutating = true;
            bool documentOffersChanged = false;
            BeginTransaction();
            try {
                ReleaseHireClaim(claim);
                _pendingHires.RemoveAt(pendingIndex);

                if (accepted) {
                    _clerks.Add(new OfficeClerkState(
                        _nextClerkId,
                        pending.ClerkName,
                        pending.ClerkAge,
                        pending.PaidPrice,
                        pending.RolledBaseMultiplier,
                        Math.Max(0d, bonusEfficiency)));
                    _nextClerkId++;
                    ReconcileClerkStatistic();
                }
                else {
                    _wallet.ReplenishWallet(pending.PaidPrice);
                }

                RequestChanged();
                documentOffersChanged = true;
                return true;
            } finally {
                try {
                    EndTransaction();
                } finally {
                    _isMutating = false;
                    if (documentOffersChanged) NotifyDocumentOffersChanged();
                }
            }
        }

        internal bool TryReleasePendingClerkHire(ClerkHireDocumentClaim claim) {
            if (!IsValidHireClaim(claim)) return false;
            ReleaseHireClaim(claim);
            NotifyDocumentOffersChanged();
            return true;
        }

        public bool HasPendingSalaryReview(int clerkId) {
            return FindPendingSalaryReviewByClerk(clerkId) >= 0;
        }

        public Value GetSalaryReviewCost(int clerkId) {
            int clerkIndex = FindClerkIndex(clerkId);
            if (clerkIndex < 0 || !_initialized) return Value.Zero;
            double ratio = SalaryReviewCostRatio;
            return ratio <= 0d ? Value.Zero : _clerks[clerkIndex].OriginalHirePrice * ratio;
        }

        public bool CanStartSalaryReview(int clerkId) {
            return CanStartSalaryReviewCore(clerkId, out _);
        }

        public bool TryStartSalaryReview(int clerkId) {
            if (!_initialized || _isMutating || _isTicking) return false;

            _isMutating = true;
            bool documentOffersChanged = false;
            BeginTransaction();
            try {
                if (!CanStartSalaryReviewCore(clerkId, out Value cost)) return false;

                var pending = new PendingSalaryReviewRecord(
                    _nextSalaryReviewRequestId,
                    clerkId,
                    cost,
                    _officeData.Value.MaximumHireSignatureMultiplier);

                bool debited = false;
                if (!cost.IsZero) {
                    Value balanceBefore = _wallet.CurrentBalance;
                    if (!_wallet.TryWithdrawWallet(cost, false) || _wallet.CurrentBalance == balanceBefore) {
                        return false;
                    }

                    debited = true;
                }

                _pendingSalaryReviews.Add(pending);
                _nextSalaryReviewRequestId++;
                if (debited) _wallet.NotifyBalanceChanged();
                RequestChanged();
                documentOffersChanged = true;
                return true;
            } finally {
                try {
                    EndTransaction();
                } finally {
                    _isMutating = false;
                    if (documentOffersChanged) NotifyDocumentOffersChanged();
                }
            }
        }

        public bool TryDismissClerk(int clerkId) {
            if (!_initialized || _isMutating || _isTicking) return false;
            int clerkIndex = FindClerkIndex(clerkId);
            if (clerkIndex < 0) return false;

            _isMutating = true;
            bool documentOffersChanged = false;
            BeginTransaction();
            try {
                _clerks.RemoveAt(clerkIndex);
                documentOffersChanged = RemoveSalaryReviewForClerk(clerkId);

                if (_clerks.Count == 0) {
                    _nextProcessingClerkIndex = 0;
                }
                else {
                    if (clerkIndex < _nextProcessingClerkIndex) _nextProcessingClerkIndex--;
                    if (_nextProcessingClerkIndex >= _clerks.Count) _nextProcessingClerkIndex = 0;
                }

                ReconcileClerkStatistic();
                RequestChanged();
                return true;
            } finally {
                try {
                    EndTransaction();
                } finally {
                    _isMutating = false;
                    if (documentOffersChanged) NotifyDocumentOffersChanged();
                }
            }
        }

        internal bool TryClaimPendingSalaryReview(out SalaryReviewDocumentClaim claim) {
            claim = default;
            return TryPeekPendingSalaryReviewDocument(out DocumentOffer offer) &&
                   long.TryParse(offer.Key.DomainId, out long requestId) &&
                   TryClaimPendingSalaryReview(requestId, out claim);
        }

        internal bool TryPeekPendingSalaryReviewDocument(out DocumentOffer offer) {
            offer = null;
            if (!_initialized || _isMutating || _isTicking) return false;

            for (int index = 0; index < _pendingSalaryReviews.Count; index++) {
                PendingSalaryReviewRecord pending = _pendingSalaryReviews[index];
                if (_claimedSalaryReviewRequestIds.Contains(pending.RequestId)) continue;
                int clerkIndex = FindClerkIndex(pending.ClerkId);
                if (clerkIndex < 0) continue;

                OfficeClerkState clerk = _clerks[clerkIndex];
                offer = new DocumentOffer(
                    new DocumentOfferKey(
                        DocumentKind.ClerkSalaryReview,
                        pending.RequestId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    true,
                    personName: clerk.Name,
                    personAge: clerk.Age,
                    amount: pending.PaidCost);
                return true;
            }

            return false;
        }

        internal bool TryClaimPendingSalaryReview(long requestedId, out SalaryReviewDocumentClaim claim) {
            claim = default;
            if (!_initialized || _isMutating || _isTicking || _nextSalaryReviewClaimToken == long.MaxValue) {
                return false;
            }

            for (int index = 0; index < _pendingSalaryReviews.Count; index++) {
                long requestId = _pendingSalaryReviews[index].RequestId;
                if (requestId != requestedId || _claimedSalaryReviewRequestIds.Contains(requestId)) continue;

                long token = ++_nextSalaryReviewClaimToken;
                _activeSalaryReviewClaims.Add(token, requestId);
                _claimedSalaryReviewRequestIds.Add(requestId);
                claim = new SalaryReviewDocumentClaim(_salaryReviewClaimEpoch, token, requestId);
                NotifyDocumentOffersChanged();
                return true;
            }

            return false;
        }

        internal bool TryCompletePendingSalaryReview(
            SalaryReviewDocumentClaim claim,
            SignatureEvaluationResult result) {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (!_initialized || _isMutating || _isTicking || !IsValidSalaryReviewClaim(claim)) return false;

            int pendingIndex = FindPendingSalaryReviewIndex(claim.RequestId);
            if (pendingIndex < 0) {
                ReleaseSalaryReviewClaim(claim);
                NotifyDocumentOffersChanged();
                return false;
            }

            PendingSalaryReviewRecord pending = _pendingSalaryReviews[pendingIndex];
            double signatureMultiplier = 1d;
            bool shouldApply = result.Status == SignatureEvaluationStatus.Accepted &&
                               TryResolveSignatureMultiplier(
                                   result,
                                   pending.MaximumSignatureMultiplier,
                                   out signatureMultiplier);

            _isMutating = true;
            bool documentOffersChanged = false;
            BeginTransaction();
            try {
                ReleaseSalaryReviewClaim(claim);
                _pendingSalaryReviews.RemoveAt(pendingIndex);

                int clerkIndex = FindClerkIndex(pending.ClerkId);
                if (shouldApply && clerkIndex >= 0) {
                    _clerks[clerkIndex].SetBonusEfficiency(Math.Max(0d, signatureMultiplier - 1d));
                }

                RequestChanged();
                documentOffersChanged = true;
                return true;
            } finally {
                try {
                    EndTransaction();
                } finally {
                    _isMutating = false;
                    if (documentOffersChanged) NotifyDocumentOffersChanged();
                }
            }
        }

        internal bool TryReleasePendingSalaryReview(SalaryReviewDocumentClaim claim) {
            if (!IsValidSalaryReviewClaim(claim)) return false;
            ReleaseSalaryReviewClaim(claim);
            NotifyDocumentOffersChanged();
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
                        bool wasAccepted = ProcessDocument(_clerks[index], entries);
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
                    ["name"] = clerk.Name,
                    ["age"] = clerk.Age,
                    ["originalHireStored"] = clerk.OriginalHirePrice.Stored,
                    ["originalHireDegree"] = clerk.OriginalHirePrice.Base.Degree,
                    ["baseEfficiency"] = clerk.BaseEfficiency,
                    ["bonusEfficiency"] = clerk.BonusEfficiency,
                    ["progress"] = clerk.Progress,
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
                    ["maximumSignatureMultiplier"] = pending.MaximumSignatureMultiplier,
                    ["clerkName"] = pending.ClerkName,
                    ["clerkAge"] = pending.ClerkAge
                });
            }

            var pendingSalaryReviews = new JArray();
            for (int index = 0; index < _pendingSalaryReviews.Count; index++) {
                PendingSalaryReviewRecord pending = _pendingSalaryReviews[index];
                pendingSalaryReviews.Add(new JObject {
                    ["requestId"] = pending.RequestId,
                    ["clerkId"] = pending.ClerkId,
                    ["paidStored"] = pending.PaidCost.Stored,
                    ["paidDegree"] = pending.PaidCost.Base.Degree,
                    ["maximumSignatureMultiplier"] = pending.MaximumSignatureMultiplier
                });
            }

            return new JObject {
                ["nextClerkId"] = _nextClerkId,
                ["nextProcessingClerkIndex"] = _nextProcessingClerkIndex,
                ["nextHireRequestId"] = _nextHireRequestId,
                ["nextSalaryReviewRequestId"] = _nextSalaryReviewRequestId,
                ["clerks"] = clerks,
                ["pendingHires"] = pendingHires,
                ["pendingSalaryReviews"] = pendingSalaryReviews
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
            bool documentOffersChanged = false;
            BeginTransaction();
            try {
                ApplyRestore(restored);
                ReconcileClerkStatistic();
                RequestChanged();
                documentOffersChanged = true;
            } finally {
                try {
                    EndTransaction();
                } finally {
                    _isMutating = false;
                    if (documentOffersChanged) NotifyDocumentOffersChanged();
                }
            }
        }

        public void Dispose() {
            _subscriptions.Dispose();
            _documentProcessed.Dispose();
            _documentOffersChanged.Dispose();
            _changed.Dispose();
            InvalidateHireClaims();
            InvalidateSalaryReviewClaims();
            _clerks.Clear();
            _pendingHires.Clear();
            _pendingSalaryReviews.Clear();
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

            double unit = SampleRandomUnit();
            double rolled = unit <= 0d
                ? lower
                : unit >= 1d
                    ? upper
                    : (1d - unit) * lower + unit * upper;
            if (double.IsPositiveInfinity(rolled)) rolled = double.MaxValue;
            if (double.IsNegativeInfinity(rolled) || double.IsNaN(rolled)) rolled = 0d;
            return Math.Max(entries.MinimumClerkMultiplier, rolled);
        }

        private string RollClerkName() {
            double unit = SampleRandomUnit();
            int index = Math.Min((int)(unit * ClerkNames.Length), ClerkNames.Length - 1);
            return ClerkNames[index];
        }

        private int RollClerkAge() {
            double unit = SampleRandomUnit();
            int ageCount = MaximumClerkAge - MinimumClerkAge + 1;
            return MinimumClerkAge + Math.Min((int)(unit * ageCount), ageCount - 1);
        }

        private double SampleRandomUnit() {
            float random = _randomValue();
            if (float.IsNaN(random) || float.IsInfinity(random)) return 0d;
            return Math.Clamp((double)random, 0d, 1d);
        }

        private bool ProcessDocument(OfficeClerkState clerk, OfficeEntries entries) {
            int selectedQuality = Math.Clamp(_documentData.Value.SelectedDocumentQualityLevel, 0, 9) + 1;
            float roll = _randomValue();
            if (float.IsNaN(roll) || float.IsInfinity(roll)) roll = 0f;
            roll = Math.Clamp(roll, 0f, 1f);
            float quality = roll * entries.QualityCeiling;
            bool accepted = quality >= entries.AcceptanceThreshold;
            double rewardFactor = accepted
                ? SaturatingMultiply(quality * entries.RewardMultiplier, clerk.IncomeMultiplier)
                : 0d;
            Value incomePerDocument = _incomeData.Value.IncomePerDocument;
            Value requested = accepted ? MultiplyValueSafely(incomePerDocument, rewardFactor) : Value.Zero;
            Value credited = accepted ? _money.AddMoney(requested) : Value.Zero;
            _documentProcessed.OnNext(new OfficeDocumentResult(
                clerk.Id,
                accepted,
                quality,
                requested,
                credited));
            if (accepted) {
                _acceptedDocuments?.Report(NormalDocumentProcessingSource.Office, selectedQuality, quality);
            }
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

        private int FindClerkIndex(int clerkId) {
            for (int index = 0; index < _clerks.Count; index++) {
                if (_clerks[index].Id == clerkId) return index;
            }

            return -1;
        }

        private int FindPendingSalaryReviewIndex(long requestId) {
            for (int index = 0; index < _pendingSalaryReviews.Count; index++) {
                if (_pendingSalaryReviews[index].RequestId == requestId) return index;
            }

            return -1;
        }

        private int FindPendingSalaryReviewByClerk(int clerkId) {
            for (int index = 0; index < _pendingSalaryReviews.Count; index++) {
                if (_pendingSalaryReviews[index].ClerkId == clerkId) return index;
            }

            return -1;
        }

        private bool CanStartSalaryReviewCore(int clerkId, out Value cost) {
            cost = Value.Zero;
            if (!_initialized || !IsUnlocked || _nextSalaryReviewRequestId == long.MaxValue ||
                FindPendingSalaryReviewByClerk(clerkId) >= 0) {
                return false;
            }

            int clerkIndex = FindClerkIndex(clerkId);
            if (clerkIndex < 0) return false;
            cost = GetSalaryReviewCost(clerkId);
            if (cost.IsZero) return true;
            if (!IsCanonicalPositiveBid(cost)) return false;

            Value balance = _wallet.CurrentBalance;
            if (!balance.IsSignificant(cost) || !_wallet.CanAfford(cost)) return false;
            Value? balanceAfterDebit = balance - cost;
            return balanceAfterDebit.HasValue && balanceAfterDebit.Value != balance;
        }

        private void InvalidateHireClaims() {
            _hireClaimEpoch++;
            _activeHireClaims.Clear();
            _claimedHireRequestIds.Clear();
        }

        private bool IsValidSalaryReviewClaim(SalaryReviewDocumentClaim claim) {
            return claim.Epoch == _salaryReviewClaimEpoch &&
                   _activeSalaryReviewClaims.TryGetValue(claim.Token, out long requestId) &&
                   requestId == claim.RequestId;
        }

        private void ReleaseSalaryReviewClaim(SalaryReviewDocumentClaim claim) {
            _activeSalaryReviewClaims.Remove(claim.Token);
            _claimedSalaryReviewRequestIds.Remove(claim.RequestId);
        }

        private void InvalidateSalaryReviewClaims() {
            _salaryReviewClaimEpoch++;
            _activeSalaryReviewClaims.Clear();
            _claimedSalaryReviewRequestIds.Clear();
        }

        private bool RemoveSalaryReviewForClerk(int clerkId) {
            int pendingIndex = FindPendingSalaryReviewByClerk(clerkId);
            if (pendingIndex < 0) return false;

            long requestId = _pendingSalaryReviews[pendingIndex].RequestId;
            _pendingSalaryReviews.RemoveAt(pendingIndex);
            _claimedSalaryReviewRequestIds.Remove(requestId);

            long activeToken = 0;
            foreach (KeyValuePair<long, long> pair in _activeSalaryReviewClaims) {
                if (pair.Value != requestId) continue;
                activeToken = pair.Key;
                break;
            }

            if (activeToken != 0) _activeSalaryReviewClaims.Remove(activeToken);
            return true;
        }

        private void NotifyDocumentOffersChanged() {
            _documentOffersChanged.OnNext(Unit.Default);
        }

        private void ApplyRestore(OfficeRestoreData restored) {
            InvalidateHireClaims();
            InvalidateSalaryReviewClaims();
            _clerks.Clear();
            for (int index = 0; index < restored.Clerks.Count; index++) {
                RestoredClerk clerk = restored.Clerks[index];
                _clerks.Add(new OfficeClerkState(
                    clerk.Id,
                    clerk.Name,
                    clerk.Age,
                    clerk.OriginalHirePrice,
                    clerk.BaseEfficiency,
                    clerk.BonusEfficiency,
                    clerk.Progress));
            }

            _pendingHires.Clear();
            _pendingHires.AddRange(restored.PendingHires);
            _pendingSalaryReviews.Clear();
            _pendingSalaryReviews.AddRange(restored.PendingSalaryReviews);
            _nextClerkId = restored.NextClerkId;
            _nextProcessingClerkIndex = restored.NextProcessingClerkIndex;
            _nextHireRequestId = restored.NextHireRequestId;
            _nextSalaryReviewRequestId = restored.NextSalaryReviewRequestId;
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

            bool hasNextSalaryReviewRequestId = root["nextSalaryReviewRequestId"] != null;
            bool hasPendingSalaryReviews = root["pendingSalaryReviews"] != null;
            if (hasPendingSalaryReviews && !hasNextSalaryReviewRequestId) {
                throw new JsonSerializationException(
                    "Office pending salary review state requires a nextSalaryReviewRequestId counter.");
            }

            if (hasPendingSalaryReviews && root["pendingSalaryReviews"] is not JArray) {
                throw new JsonSerializationException("Office pending salary review state must be an array.");
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

                int id = data["id"].Value<int>();
                if (id <= 0 || !clerkIds.Add(id) || float.IsNaN(progress) || float.IsInfinity(progress) ||
                    progress < 0f || progress > 1f) {
                    throw new JsonSerializationException(
                        $"Office clerk at index {index} has an invalid ID or progress.");
                }

                bool hasName = data["name"] != null;
                bool hasAge = data["age"] != null;
                bool hasOriginalStored = data["originalHireStored"] != null;
                bool hasOriginalDegree = data["originalHireDegree"] != null;
                bool hasBase = data["baseEfficiency"] != null;
                bool hasBonus = data["bonusEfficiency"] != null;
                int newFieldCount = (hasName ? 1 : 0) + (hasAge ? 1 : 0) +
                                    (hasOriginalStored ? 1 : 0) + (hasOriginalDegree ? 1 : 0) +
                                    (hasBase ? 1 : 0) + (hasBonus ? 1 : 0);

                string name;
                int age;
                Value originalHirePrice;
                double baseEfficiency;
                double bonusEfficiency;
                if (newFieldCount == 0) {
                    double legacyIncomeMultiplier = 1d;
                    if (data["incomeMultiplier"] != null &&
                        !TryReadNumber(data["incomeMultiplier"], out legacyIncomeMultiplier)) {
                        throw new JsonSerializationException(
                            $"Office legacy clerk at index {index} has a non-numeric income multiplier.");
                    }

                    if (!IsFiniteNonNegative(legacyIncomeMultiplier)) {
                        throw new JsonSerializationException(
                            $"Office legacy clerk at index {index} has an invalid income multiplier.");
                    }

                    name = ResolveFallbackClerkName(id);
                    age = ResolveFallbackClerkAge(id);
                    originalHirePrice = Value.One;
                    baseEfficiency = legacyIncomeMultiplier;
                    bonusEfficiency = 0d;
                }
                else if (newFieldCount == 6 && data["name"]?.Type == JTokenType.String &&
                         data["age"]?.Type == JTokenType.Integer &&
                         TryReadValue(data["originalHireStored"], data["originalHireDegree"],
                             out originalHirePrice) &&
                         TryReadNumber(data["baseEfficiency"], out baseEfficiency) &&
                         TryReadNumber(data["bonusEfficiency"], out bonusEfficiency)) {
                    name = data["name"].Value<string>();
                    age = data["age"].Value<int>();
                    if (!IsValidClerkProfile(name, age) || !IsFiniteNonNegative(baseEfficiency) ||
                        !IsFiniteNonNegative(bonusEfficiency)) {
                        throw new JsonSerializationException(
                            $"Office clerk at index {index} has invalid profile or efficiency values.");
                    }
                }
                else {
                    throw new JsonSerializationException(
                        $"Office clerk at index {index} contains a partial new-format record.");
                }

                maximumClerkId = Math.Max(maximumClerkId, id);
                clerks.Add(new RestoredClerk(
                    id,
                    name,
                    age,
                    originalHirePrice,
                    baseEfficiency,
                    bonusEfficiency,
                    progress));
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
            if (hasNextHireRequestId &&
                (!TryReadInt64(root["nextHireRequestId"], out nextHireRequestId) || nextHireRequestId <= 0)) {
                throw new JsonSerializationException("Office next hire request ID must be a positive integer.");
            }

            var pendingHires = new List<PendingClerkHireRecord>();
            var hireRequestIds = new HashSet<long>();
            long maximumHireRequestId = 0;
            if (root["pendingHires"] is JArray pendingHireToken) {
                pendingHires.Capacity = pendingHireToken.Count;
                for (int index = 0; index < pendingHireToken.Count; index++) {
                    if (pendingHireToken[index] is not JObject data ||
                        !TryReadInt64(data["requestId"], out long requestId) ||
                        !TryReadValue(data["paidStored"], data["paidDegree"], out Value paidPrice) ||
                        !TryReadNumber(data["rolledBaseMultiplier"], out double rolledBaseMultiplier) ||
                        !TryReadNumber(data["maximumSignatureMultiplier"], out double maximumSignatureMultiplier)) {
                        throw new JsonSerializationException(
                            $"Office pending hire at index {index} is missing required canonical values.");
                    }

                    bool hasName = data["clerkName"] != null;
                    bool hasAge = data["clerkAge"] != null;
                    string clerkName;
                    int clerkAge;
                    if (!hasName && !hasAge) {
                        clerkName = ResolveFallbackClerkName(requestId);
                        clerkAge = ResolveFallbackClerkAge(requestId);
                    }
                    else if (hasName && hasAge && data["clerkName"].Type == JTokenType.String &&
                             data["clerkAge"].Type == JTokenType.Integer) {
                        clerkName = data["clerkName"].Value<string>();
                        clerkAge = data["clerkAge"].Value<int>();
                    }
                    else {
                        throw new JsonSerializationException(
                            $"Office pending hire at index {index} contains a partial clerk profile.");
                    }

                    if (requestId <= 0 || !hireRequestIds.Add(requestId) ||
                        !IsFiniteNonNegative(rolledBaseMultiplier) ||
                        !IsFiniteAtLeastOne(maximumSignatureMultiplier) ||
                        !IsValidClerkProfile(clerkName, clerkAge)) {
                        throw new JsonSerializationException(
                            $"Office pending hire at index {index} has values outside valid ranges.");
                    }

                    maximumHireRequestId = Math.Max(maximumHireRequestId, requestId);
                    pendingHires.Add(new PendingClerkHireRecord(
                        requestId,
                        paidPrice,
                        rolledBaseMultiplier,
                        maximumSignatureMultiplier,
                        clerkName,
                        clerkAge));
                }
            }

            if (nextHireRequestId <= maximumHireRequestId) {
                throw new JsonSerializationException(
                    "Office next hire request ID must be greater than every restored request ID.");
            }

            long nextSalaryReviewRequestId = 1;
            if (hasNextSalaryReviewRequestId &&
                (!TryReadInt64(root["nextSalaryReviewRequestId"], out nextSalaryReviewRequestId) ||
                 nextSalaryReviewRequestId <= 0)) {
                throw new JsonSerializationException(
                    "Office next salary review request ID must be a positive integer.");
            }

            var pendingSalaryReviews = new List<PendingSalaryReviewRecord>();
            var salaryRequestIds = new HashSet<long>();
            var reviewedClerkIds = new HashSet<int>();
            long maximumSalaryReviewRequestId = 0;
            if (root["pendingSalaryReviews"] is JArray pendingSalaryToken) {
                pendingSalaryReviews.Capacity = pendingSalaryToken.Count;
                for (int index = 0; index < pendingSalaryToken.Count; index++) {
                    if (pendingSalaryToken[index] is not JObject data ||
                        !TryReadInt64(data["requestId"], out long requestId) ||
                        data["clerkId"]?.Type != JTokenType.Integer ||
                        !TryReadNonNegativeValue(data["paidStored"], data["paidDegree"], out Value paidCost) ||
                        !TryReadNumber(data["maximumSignatureMultiplier"], out double maximumSignatureMultiplier)) {
                        throw new JsonSerializationException(
                            $"Office pending salary review at index {index} is missing required canonical values.");
                    }

                    int clerkId = data["clerkId"].Value<int>();
                    if (requestId <= 0 || !salaryRequestIds.Add(requestId) || !clerkIds.Contains(clerkId) ||
                        !reviewedClerkIds.Add(clerkId) || !IsFiniteAtLeastOne(maximumSignatureMultiplier)) {
                        throw new JsonSerializationException(
                            $"Office pending salary review at index {index} has invalid or duplicate references.");
                    }

                    maximumSalaryReviewRequestId = Math.Max(maximumSalaryReviewRequestId, requestId);
                    pendingSalaryReviews.Add(new PendingSalaryReviewRecord(
                        requestId,
                        clerkId,
                        paidCost,
                        maximumSignatureMultiplier));
                }
            }

            if (nextSalaryReviewRequestId <= maximumSalaryReviewRequestId) {
                throw new JsonSerializationException(
                    "Office next salary review request ID must be greater than every restored request ID.");
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
                nextSalaryReviewRequestId,
                clerks,
                pendingHires,
                pendingSalaryReviews);
        }

        private static double ResolveSignatureMultiplier(
            SignatureEvaluationResult result,
            double maximumSignatureMultiplier) {
            return TryResolveSignatureMultiplier(result, maximumSignatureMultiplier, out double multiplier)
                ? multiplier
                : 1d;
        }

        private static bool TryResolveSignatureMultiplier(
            SignatureEvaluationResult result,
            double maximumSignatureMultiplier,
            out double multiplier) {
            multiplier = 1d;
            float similarity = result.Similarity;
            float minimum = result.MinimumSimilarity;
            bool malformed = float.IsNaN(similarity) || float.IsInfinity(similarity) ||
                              float.IsNaN(minimum) || float.IsInfinity(minimum) ||
                              similarity < 0f || similarity > 1f || minimum < 0f || minimum > 1f ||
                              similarity < minimum;
            if (malformed) return false;
            if (minimum >= 1f) {
                multiplier = maximumSignatureMultiplier;
                return true;
            }

            double quality = Math.Clamp((similarity - minimum) / (1d - minimum), 0d, 1d);
            multiplier = 1d + (maximumSignatureMultiplier - 1d) * quality;
            return !double.IsNaN(multiplier) && !double.IsInfinity(multiplier) && multiplier >= 1d;
        }

        private static string ResolveFallbackClerkName(long seed) {
            ulong value = unchecked((ulong)seed * 11400714819323198485UL);
            return ClerkNames[(int)(value % (ulong)ClerkNames.Length)];
        }

        private static int ResolveFallbackClerkAge(long seed) {
            ulong value = unchecked((ulong)seed * 7046029254386353131UL);
            int ageCount = MaximumClerkAge - MinimumClerkAge + 1;
            return MinimumClerkAge + (int)(value % (ulong)ageCount);
        }

        private static bool IsValidClerkProfile(string name, int age) {
            return !string.IsNullOrWhiteSpace(name) && name.Length <= MaximumClerkNameLength &&
                   age >= MinimumClerkAge && age <= MaximumClerkAge;
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

        private static bool TryReadNonNegativeValue(JToken storedToken, JToken degreeToken, out Value value) {
            value = default;
            if (!TryReadNumber(storedToken, out double stored) || degreeToken?.Type != JTokenType.Integer) {
                return false;
            }

            int degree = degreeToken.Value<int>();
            if (double.IsNaN(stored) || double.IsInfinity(stored) || stored < 0d || stored >= 1000d ||
                degree < 0 || degree == int.MaxValue || stored == 0d && degree != 0 ||
                degree > 0 && stored < 1d) {
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
            public long NextSalaryReviewRequestId { get; }
            public List<RestoredClerk> Clerks { get; }
            public List<PendingClerkHireRecord> PendingHires { get; }
            public List<PendingSalaryReviewRecord> PendingSalaryReviews { get; }

            public OfficeRestoreData(
                int nextClerkId,
                int nextProcessingClerkIndex,
                long nextHireRequestId,
                long nextSalaryReviewRequestId,
                List<RestoredClerk> clerks,
                List<PendingClerkHireRecord> pendingHires,
                List<PendingSalaryReviewRecord> pendingSalaryReviews) {
                NextClerkId = nextClerkId;
                NextProcessingClerkIndex = nextProcessingClerkIndex;
                NextHireRequestId = nextHireRequestId;
                NextSalaryReviewRequestId = nextSalaryReviewRequestId;
                Clerks = clerks;
                PendingHires = pendingHires;
                PendingSalaryReviews = pendingSalaryReviews;
            }
        }

        private readonly struct RestoredClerk {
            public int Id { get; }
            public string Name { get; }
            public int Age { get; }
            public Value OriginalHirePrice { get; }
            public double BaseEfficiency { get; }
            public double BonusEfficiency { get; }
            public float Progress { get; }

            public RestoredClerk(
                int id,
                string name,
                int age,
                Value originalHirePrice,
                double baseEfficiency,
                double bonusEfficiency,
                float progress) {
                Id = id;
                Name = name;
                Age = age;
                OriginalHirePrice = originalHirePrice;
                BaseEfficiency = baseEfficiency;
                BonusEfficiency = bonusEfficiency;
                Progress = progress;
            }
        }

        private readonly struct PendingClerkHireRecord {
            public long RequestId { get; }
            public Value PaidPrice { get; }
            public double RolledBaseMultiplier { get; }
            public double MaximumSignatureMultiplier { get; }
            public string ClerkName { get; }
            public int ClerkAge { get; }

            public PendingClerkHireRecord(
                long requestId,
                Value paidPrice,
                double rolledBaseMultiplier,
                double maximumSignatureMultiplier,
                string clerkName,
                int clerkAge) {
                RequestId = requestId;
                PaidPrice = paidPrice;
                RolledBaseMultiplier = rolledBaseMultiplier;
                MaximumSignatureMultiplier = maximumSignatureMultiplier;
                ClerkName = clerkName;
                ClerkAge = clerkAge;
            }
        }

        private readonly struct PendingSalaryReviewRecord {
            public long RequestId { get; }
            public int ClerkId { get; }
            public Value PaidCost { get; }
            public double MaximumSignatureMultiplier { get; }

            public PendingSalaryReviewRecord(
                long requestId,
                int clerkId,
                Value paidCost,
                double maximumSignatureMultiplier) {
                RequestId = requestId;
                ClerkId = clerkId;
                PaidCost = paidCost;
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

        internal readonly struct SalaryReviewDocumentClaim {
            internal int Epoch { get; }
            internal long Token { get; }
            internal long RequestId { get; }

            internal SalaryReviewDocumentClaim(int epoch, long token, long requestId) {
                Epoch = epoch;
                Token = token;
                RequestId = requestId;
            }
        }
    }
}
