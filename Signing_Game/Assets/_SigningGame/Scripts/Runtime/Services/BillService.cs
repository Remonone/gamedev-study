using System;
using System.Collections.Generic;
using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Bills;
using Data.Cache;
using Data.Documents;
using Data.Enums;
using Data.Modifiers;
using Data.Modifiers.Calculation;
using Data.Results;
using R3;
using Services.Calculators;
using Services.Locator;
using UnityEngine;
using Utils;

namespace Services {
    public sealed partial class BillService : IService, IInitialize, IPostInitialize, ISaveable {
        private const double MaximumValueLog10 = (double)int.MaxValue * 3d;
        private readonly Subject<Unit> _changed = new();
        private readonly Subject<Unit> _documentOffersChanged = new();
        private readonly CompositeDisposable _subscriptions = new();
        private readonly Dictionary<long, long> _activeClaims = new();

        private Dictionary<string, BillRewardDefinition> _rewards = new(StringComparer.Ordinal);
        private Dictionary<string, BillRequirementTemplateDefinition> _templates = new(StringComparer.Ordinal);
        private Dictionary<string, BillRequirementPresentationInfo> _requirementPresentation =
            new(StringComparer.Ordinal);
        private List<GeneratedBillOption> _catalog = new();
        private PendingBillState _pending;
        private List<ActiveBillState> _active = new();
        private List<BillCompletionRecord> _completed = new();

        private UnlockService _unlocks;
        private IAssetProvider _assetProvider;
        private IAssetListLease<BillCatalogDefinition> _catalogLease;
        private IBillRandom _random;
        private WalletService _wallet;
        private UpgradeService _upgrades;
        private OfficeService _office;
        private PlayerStatStash _stash;
        private IReadOnlyCacheData<BillEntries> _billData;
        private ICacheInvalidator _cacheInvalidator;
        private CacheVersionService _cacheVersions;
        private DocumentCacheCalculator _documentCalculator;
        private GenerationCacheCalculator _generationCalculator;
        private IncomeCacheCalculator _incomeCalculator;
        private AcceptedNormalDocumentService _acceptedDocuments;
        private DocumentGeneratorService _documentGenerator;
        private MoneyAggregator _moneyAggregator;

        private double[] _generationAttributionShares = Array.Empty<double>();
        private double[] _incomeAttributionShares = Array.Empty<double>();
        private double _generationAttributionShareSum;
        private double _incomeAttributionShareSum;
        private double _pendingGeneratedDocumentEquivalents;
        private Value _pendingCreditedIncome;
        private bool _contributionDirty;
        private float _contributionNotificationDelay;

        private RestoreData _deferredRestore;
        private bool _postInitialized;
        private bool _isMutating;
        private bool _handlingInvalidation;
        private bool _ignoreWalletNotification;
        private int _observedClerkCount;
        private int _claimEpoch;
        private long _nextClaimToken;
        private long _nextOptionId = 1;
        private long _nextInstanceId = 1;
        private long _nextActivationOrder = 1;
        private long _nextCompletionOrder = 1;

        public string SaveId => "Bills";
        public IReadOnlyList<GeneratedBillOption> Catalog => _catalog;
        public PendingBillState Pending => _pending;
        public IReadOnlyList<ActiveBillState> ActiveBills => _active;
        public IReadOnlyList<BillCompletionRecord> CompletedBills => _completed;
        public Observable<Unit> Changed => _changed;
        public Observable<Unit> DocumentOffersChanged => _documentOffersChanged;
        public int ActiveProjectLimit => _postInitialized ? ResolveActiveLimit(_billData.Value) : 1;
        public int MaximumPriorityWeight => _postInitialized ? ResolveMaximumPriorityWeight(_billData.Value) : 1;
        public bool IsUnlocked => _unlocks?.IsUnlocked(FeatureIds.BillCatalog) ?? false;

        internal IReadOnlyList<BillCompletionRecord> CompletionRecords => _completed;
        internal bool HasActiveBills => _active.Count > 0;

        public BillService() : this(null, null) { }

        internal BillService(IAssetProvider assetProvider, IBillRandom random) {
            _assetProvider = assetProvider;
            _random = random ?? new BillRandom(unchecked((ulong)DateTime.UtcNow.Ticks));
        }

        public async UniTask InitializeAsync(IServiceScope scope) {
            _assetProvider ??= scope.Container.Get<IAssetProvider>();
            _catalogLease = await _assetProvider.LoadAssetsByLabelAsync<BillCatalogDefinition>(
                AddressableConstants.BILL_CATALOG_LABEL);
            if (_catalogLease.Assets.Count != 1) {
                throw new InvalidOperationException(
                    $"Exactly one BillCatalogDefinition is required, found {_catalogLease.Assets.Count}.");
            }

            BuildDefinitions(_catalogLease.Assets[0]);
        }

        public UniTask PostInitializeAsync(IServiceScope scope) {
            _wallet = scope.Get<WalletService>();
            _upgrades = scope.Get<UpgradeService>();
            _office = scope.Get<OfficeService>();
            _stash = scope.Get<PlayerStatStash>();
            _billData = _stash.BillData;
            _cacheInvalidator = scope.Get<ICacheInvalidator>();
            _cacheVersions = scope.Get<CacheVersionService>();
            _documentCalculator = scope.Get<DocumentCacheCalculator>();
            _acceptedDocuments = scope.Get<AcceptedNormalDocumentService>();
            _unlocks = scope.Get<UnlockService>();
            scope.TryGet(out _generationCalculator);
            scope.TryGet(out _incomeCalculator);
            scope.TryGet(out _documentGenerator);
            scope.TryGet(out _moneyAggregator);
            _postInitialized = true;

            if (_deferredRestore != null) {
                RestoreData restore = _deferredRestore;
                _deferredRestore = null;
                try {
                    ApplyRestore(restore, false);
                }
                catch (Exception exception) {
                    Debug.LogWarning(
                        $"Failed to apply the deferred Bills save section. Default bill state will be used.\n{exception}");
                    ResetToDefaultState();
                }
            }
            else if (_rewards.Count == 0) {
                Debug.LogWarning("Bill catalog contains no rewards. The bill system started with an empty catalog.");
            }
            else {
                ReplaceCatalog(BuildFreshCatalog(CreateStateView(null, _active, _completed), _billData.Value));
            }

            _acceptedDocuments.Processed.Subscribe(ProcessAcceptedDocument).AddTo(_subscriptions);
            _wallet.BalanceChanged.Subscribe(_ => {
                if (!_ignoreWalletNotification) NotifyChanged();
            }).AddTo(_subscriptions);
            _upgrades.Changed.Subscribe(_ => ReconcileAfterExternalStateChange()).AddTo(_subscriptions);
            _observedClerkCount = _office.ClerkCount;
            _unlocks.Changed.Where(_ => _unlocks.IsUnlocked(FeatureIds.BillCatalog)).Subscribe(_ => NotifyChanged()).AddTo(_subscriptions);
            _office.Changed.Subscribe(_ => {
                int clerkCount = _office.ClerkCount;
                if (clerkCount == _observedClerkCount) return;
                _observedClerkCount = clerkCount;
                ReconcileAfterExternalStateChange();
            }).AddTo(_subscriptions);
            _cacheVersions.Invalidated.Subscribe(OnCacheInvalidated).AddTo(_subscriptions);
            Observable.EveryUpdate().Select(_ => Time.deltaTime).Subscribe(OnUpdate).AddTo(_subscriptions);
            if (_documentGenerator != null) {
                _documentGenerator.WorkGenerated.Subscribe(AccumulateGenerationWork).AddTo(_subscriptions);
                _documentGenerator.DocumentsGenerated.Subscribe(_ => MarkContributionDirty()).AddTo(_subscriptions);
            }
            if (_moneyAggregator != null) {
                _moneyAggregator.MoneyAdded.Subscribe(AccumulateMoneyTransaction).AddTo(_subscriptions);
            }
            RebuildAttributionShares();
            return UniTask.CompletedTask;
        }

        internal void BuildDefinitions(BillCatalogDefinition catalog) {
            if (catalog == null) throw new InvalidOperationException("Bill catalog asset is null.");

            var rewards = new Dictionary<string, BillRewardDefinition>(StringComparer.Ordinal);
            BillRewardDefinition[] rewardDefinitions = catalog.Rewards ?? Array.Empty<BillRewardDefinition>();
            for (int index = 0; index < rewardDefinitions.Length; index++) {
                BillRewardDefinition reward = rewardDefinitions[index];
                ValidateReward(reward);
                if (!rewards.TryAdd(reward.Id, reward)) {
                    throw new InvalidOperationException($"Duplicate bill reward ID '{reward.Id}'.");
                }
            }

            var templates = new Dictionary<string, BillRequirementTemplateDefinition>(StringComparer.Ordinal);
            var presentation = new Dictionary<string, BillRequirementPresentationInfo>(StringComparer.Ordinal);
            BillRequirementTemplateDefinition[] templateDefinitions =
                catalog.RequirementTemplates ?? Array.Empty<BillRequirementTemplateDefinition>();
            for (int index = 0; index < templateDefinitions.Length; index++) {
                BillRequirementTemplateDefinition template = templateDefinitions[index];
                ValidateTemplate(template);
                if (!templates.TryAdd(template.Id, template)) {
                    throw new InvalidOperationException($"Duplicate bill requirement template ID '{template.Id}'.");
                }
                presentation.Add(template.Id, new BillRequirementPresentationInfo(
                    template.DisplayName,
                    template.ShortDescription,
                    template.Color));
            }

            if (rewards.Count > 0) {
                bool hasFallback = false;
                foreach (BillRewardDefinition reward in rewards.Values) {
                    if (reward.Repeatable && reward.MinimumRequirementCount == 0) {
                        hasFallback = true;
                        break;
                    }
                }

                if (!hasFallback) {
                    throw new InvalidOperationException(
                        "A non-empty bill catalog requires a repeatable reward with zero minimum requirements.");
                }
            }

            _rewards = rewards;
            _templates = templates;
            _requirementPresentation = presentation;
        }

        public bool TryGetRequirementPresentation(
            string templateId,
            out BillRequirementPresentationInfo presentation) {
            if (string.IsNullOrWhiteSpace(templateId)) {
                presentation = default;
                return false;
            }
            return _requirementPresentation.TryGetValue(templateId, out presentation);
        }

        public Value ResolvePrice(GeneratedBillOption option) {
            if (option == null) throw new ArgumentNullException(nameof(option));
            return ResolvePrice(option, _billData.Value);
        }

        public double ResolveCatalogBaseRewardStrength(GeneratedBillOption option) {
            if (option == null) throw new ArgumentNullException(nameof(option));
            if (!_postInitialized) return 1d;
            return ResolveRequirementStrength(option, _billData.Value);
        }

        public double ResolveCompletionEffectiveness(double savedBaseRewardStrength) {
            if (!_postInitialized) return Math.Max(0d, savedBaseRewardStrength);
            return SaturatingMultiplyPositive(
                savedBaseRewardStrength,
                Math.Max(0d, _billData.Value.OverallRewardMultiplier));
        }

        public double ResolveActiveGenerationBonus(GeneratedBillOption option, double baseRewardStrength) {
            if (option == null) throw new ArgumentNullException(nameof(option));
            if (!_postInitialized) return 0d;
            BillEntries entries = _billData.Value;
            double bonus = SaturatingMultiplyPositive(option.Reward.BaseActiveGenerationBonus, baseRewardStrength);
            bonus = SaturatingMultiplyPositive(bonus, Math.Max(0d, entries.OverallRewardMultiplier));
            return SaturatingMultiplyPositive(bonus, Math.Max(0d, entries.ActiveGenerationBonusMultiplier));
        }

        public Value ResolveExpectedCompletionPayout(GeneratedBillOption option, double baseRewardStrength) {
            if (option == null) throw new ArgumentNullException(nameof(option));
            if (option.Reward.MoneyReward.IsZero || !_postInitialized) return Value.Zero;
            return MultiplyValueSafely(
                option.Reward.MoneyReward,
                ResolveCompletionEffectiveness(baseRewardStrength));
        }

        public bool AreRequirementsSatisfied(GeneratedBillOption option) {
            if (option == null || !_postInitialized) return false;
            int unlockedQuality = ResolveCandidateUnlockedQuality(_completed, _billData.Value);
            return AreRequirementsSatisfied(option, CaptureExternalState(unlockedQuality));
        }

        public bool IsRequirementSatisfied(GeneratedBillOption option, BillRequirementSnapshot requirement) {
            if (option == null || requirement == null || !_postInitialized) return false;
            bool belongsToOption = false;
            for (int index = 0; index < option.Requirements.Count; index++) {
                if (ReferenceEquals(option.Requirements[index], requirement)) {
                    belongsToOption = true;
                    break;
                }
            }
            if (!belongsToOption) return false;
            int unlockedQuality = ResolveCandidateUnlockedQuality(_completed, _billData.Value);
            return IsRequirementSatisfied(requirement, CaptureExternalState(unlockedQuality));
        }

        public IReadOnlyList<BillPurchaseBlocker> GetPurchaseBlockers(long optionId) {
            var blockers = new List<BillPurchaseBlocker>();
            if (!_postInitialized || _isMutating) {
                blockers.Add(new BillPurchaseBlocker(BillPurchaseBlockerKind.Unavailable));
                return blockers;
            }
            GeneratedBillOption option = FindCatalogOption(optionId);
            if (option == null) {
                blockers.Add(new BillPurchaseBlocker(BillPurchaseBlockerKind.Unavailable));
                return blockers;
            }
            if (_pending != null) blockers.Add(new BillPurchaseBlocker(BillPurchaseBlockerKind.PendingSignature));
            BillEntries entries = _billData.Value;
            if (_active.Count >= ResolveActiveLimit(entries)) {
                blockers.Add(new BillPurchaseBlocker(BillPurchaseBlockerKind.ActiveLimitReached));
            }
            int unlockedQuality = ResolveCandidateUnlockedQuality(_completed, entries);
            ExternalState external = CaptureExternalState(unlockedQuality);
            for (int index = 0; index < option.Requirements.Count; index++) {
                BillRequirementSnapshot requirement = option.Requirements[index];
                if (!IsRequirementSatisfied(requirement, external)) {
                    blockers.Add(new BillPurchaseBlocker(
                        BillPurchaseBlockerKind.RequirementNotMet,
                        requirement));
                }
            }
            Value price = ResolvePrice(option, entries);
            if (!_wallet.CanAfford(price)) {
                Value missing = price > _wallet.CurrentBalance
                    ? (price - _wallet.CurrentBalance).Value
                    : Value.Zero;
                blockers.Add(new BillPurchaseBlocker(BillPurchaseBlockerKind.InsufficientFunds, missingFunds: missing));
            }
            return blockers;
        }

        public bool CanPurchase(long optionId) {
            if (!_postInitialized || _isMutating || _pending != null) return false;
            GeneratedBillOption option = FindCatalogOption(optionId);
            if (option == null) return false;

            BillEntries entries = _billData.Value;
            if (_active.Count >= ResolveActiveLimit(entries)) return false;
            int unlockedQuality = ResolveCandidateUnlockedQuality(_completed, entries);
            if (!AreRequirementsSatisfied(option, CaptureExternalState(unlockedQuality))) return false;

            Value price = ResolvePrice(option, entries);
            if (!_wallet.CanAfford(price)) return false;

            return true;
        }

        public bool TryPurchase(long optionId) {
            if (!CanPurchase(optionId)) return false;

            GeneratedBillOption option = FindCatalogOption(optionId);
            BillEntries entries = _billData.Value;
            Value price = ResolvePrice(option, entries);
            var pending = new PendingBillState(option, price);

            _isMutating = true;
            try {
                if (!_wallet.TryWithdrawWallet(price, false)) return false;
                _pending = pending;
                ReplaceCatalog(CreateSuppressedCatalogBuild());
                InvalidateClaims();
            }
            finally {
                _isMutating = false;
            }

            PublishWalletChanged();
            NotifyChanged();
            NotifyDocumentOffersChanged();
            return true;
        }

        public bool TrySetPriorityWeight(long instanceId, int weight) {
            if (!_postInitialized || _isMutating) return false;
            int maximum = ResolveMaximumPriorityWeight(_billData.Value);
            if (weight < 1 || weight > maximum) return false;
            ActiveBillState active = FindActive(instanceId);
            if (active == null || active.Weight == weight) return active != null;
            active.Weight = weight;
            NotifyChanged();
            return true;
        }

        internal bool TryPeekPendingDocument(out DocumentOffer offer) {
            offer = null;
            if (_pending == null || HasClaimForPending()) return false;
            GeneratedBillOption option = _pending.Option;
            offer = new DocumentOffer(
                new DocumentOfferKey(DocumentKind.Bill, option.OptionId.ToString()),
                true,
                option.Reward.Name,
                option.Reward.Icon,
                amount: _pending.PaidCost);
            return true;
        }

        internal bool TryClaimPending(string domainId, out BillDocumentClaim claim) {
            claim = default;
            if (_pending == null || HasClaimForPending() ||
                !long.TryParse(domainId, out long optionId) || optionId != _pending.Option.OptionId) {
                return false;
            }

            long token = ++_nextClaimToken;
            _activeClaims.Add(token, optionId);
            claim = new BillDocumentClaim(_claimEpoch, token, optionId, _pending.Option.SignatureThreshold);
            NotifyDocumentOffersChanged();
            return true;
        }

        internal bool TryReleaseClaim(BillDocumentClaim claim) {
            if (!IsValidClaim(claim)) return false;
            _activeClaims.Remove(claim.Token);
            NotifyDocumentOffersChanged();
            return true;
        }

        internal bool TryProcessClaim(BillDocumentClaim claim, SignatureEvaluationResult result) {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (_isMutating || !IsValidClaim(claim) || _pending == null ||
                _pending.Option.OptionId != claim.OptionId) {
                return false;
            }

            BillEntries entries = _billData.Value;
            if (result.Status != SignatureEvaluationStatus.Accepted) {
                StateView rejectedState = CreateStateView(null, _active, _completed);
                if (!TryPrepareCatalog(rejectedState, entries, true, out CatalogBuild rejectedBuild)) {
                    Debug.LogError("Bill rejection could not regenerate a valid catalog.");
                    return false;
                }

                _isMutating = true;
                try {
                    _pending = null;
                    ReplaceCatalog(rejectedBuild);
                    _activeClaims.Remove(claim.Token);
                }
                finally {
                    _isMutating = false;
                }

                NotifyChanged();
                NotifyDocumentOffersChanged();
                return true;
            }

            double baseStrength = ResolveBaseRewardStrength(_pending.Option, result, entries);
            var nextActive = CloneActive(_active);
            nextActive.Add(new ActiveBillState(
                _nextInstanceId,
                _pending.Option,
                0d,
                1,
                0L,
                _nextActivationOrder,
                baseStrength,
                _pending.PaidCost,
                0d,
                0,
                true));
            StateView acceptedState = CreateStateView(null, nextActive, _completed);
            if (!TryPrepareCatalog(acceptedState, entries, false, out CatalogBuild acceptedBuild)) return false;

            _isMutating = true;
            try {
                _nextInstanceId++;
                _nextActivationOrder++;
                _pending = null;
                _active = nextActive;
                ReplaceCatalog(acceptedBuild);
                _activeClaims.Remove(claim.Token);
                InvalidateActiveCaches();
            }
            finally {
                _isMutating = false;
            }

            NotifyChanged();
            NotifyDocumentOffersChanged();
            return true;
        }

        internal double GetStrongestActiveGenerationBonus(BillEntries entries) {
            double strongest = 0d;
            double overall = Math.Max(0d, entries.OverallRewardMultiplier);
            double activeMultiplier = Math.Max(0d, entries.ActiveGenerationBonusMultiplier);
            for (int index = 0; index < _active.Count; index++) {
                ActiveBillState active = _active[index];
                double bonus = SaturatingMultiplyPositive(
                    active.Option.Reward.BaseActiveGenerationBonus,
                    active.SavedBaseRewardStrength);
                bonus = SaturatingMultiplyPositive(bonus, overall);
                bonus = SaturatingMultiplyPositive(bonus, activeMultiplier);
                if (bonus > strongest) strongest = bonus;
            }

            return strongest;
        }

        private void ProcessAcceptedDocument(AcceptedNormalDocument document) {
            if (_isMutating || _active.Count == 0) return;

            BillEntries entries = _billData.Value;
            var nextActive = CloneActive(_active);
            int selectedIndex = SelectWeighted(nextActive);
            if (selectedIndex < 0) return;

            ActiveBillState selected = nextActive[selectedIndex];
            if (selected.ProcessedDocumentCount < int.MaxValue) selected.ProcessedDocumentCount++;
            double increment = ResolveProgressIncrement(selected.Option, document.SelectedQuality);
            if (increment <= 0d || !IsFinite(increment)) {
                _active = nextActive;
                NotifyChanged();
                return;
            }

            selected.Progress = Math.Min(selected.Option.RequiredProgress, selected.Progress + increment);
            bool completed = selected.Progress >= selected.Option.RequiredProgress;
            if (!completed) {
                _active = nextActive;
                NotifyChanged();
                return;
            }

            double moneyMultiplier = SaturatingMultiplyPositive(
                selected.SavedBaseRewardStrength,
                Math.Max(0d, entries.OverallRewardMultiplier));
            Value money = selected.Option.Reward.MoneyReward.IsZero
                ? Value.Zero
                : MultiplyValueSafely(selected.Option.Reward.MoneyReward, moneyMultiplier);

            MaterializePendingContributions(false);
            nextActive.RemoveAt(selectedIndex);
            var provisional = new BillCompletionRecord(
                selected.Option,
                selected.Option.Reward,
                selected.SavedBaseRewardStrength,
                _nextCompletionOrder,
                selected.PaidCost,
                selected.ElapsedWorkSeconds,
                selected.ProcessedDocumentCount,
                selected.HasCompleteWorkStatistics,
                money,
                true,
                0d,
                Value.Zero);
            var candidateCompleted = new List<BillCompletionRecord>(_completed) { provisional };
            StateView candidate = CreateStateView(_pending, nextActive, candidateCompleted);
            if (!TryPrepareCatalog(candidate, entries, false, out CatalogBuild build)) return;

            _isMutating = true;
            bool walletChanged = false;
            try {
                Value before = _wallet.CurrentBalance;
                walletChanged = !money.IsZero && _wallet.ReplenishWallet(money, false);
                Value after = _wallet.CurrentBalance;
                Value actualPayout = walletChanged && after > before
                    ? (after - before).Value
                    : Value.Zero;
                var completedRecord = new BillCompletionRecord(
                    selected.Option,
                    selected.Option.Reward,
                    selected.SavedBaseRewardStrength,
                    _nextCompletionOrder,
                    selected.PaidCost,
                    selected.ElapsedWorkSeconds,
                    selected.ProcessedDocumentCount,
                    selected.HasCompleteWorkStatistics,
                    actualPayout,
                    true,
                    0d,
                    Value.Zero);
                var nextCompleted = new List<BillCompletionRecord>(_completed) { completedRecord };
                _nextCompletionOrder++;
                _active = nextActive;
                _completed = nextCompleted;
                ReplaceCatalog(build);
                InvalidateActiveCaches();
                InvalidateCompletionGroups(selected.Option.Reward);
                RebuildAttributionShares();
            }
            finally {
                _isMutating = false;
            }

            if (walletChanged) PublishWalletChanged();
            NotifyChanged();
        }

        private int SelectWeighted(List<ActiveBillState> active) {
            if (active.Count == 0) return -1;
            long totalWeight = 0L;
            int selected = 0;
            for (int index = 0; index < active.Count; index++) {
                int weight = Math.Max(1, active[index].Weight);
                totalWeight += weight;
                if (active[index].SchedulerCurrentWeight > long.MaxValue - weight) {
                    for (int reset = 0; reset < active.Count; reset++) active[reset].SchedulerCurrentWeight = 0L;
                }
                active[index].SchedulerCurrentWeight += weight;
                if (active[index].SchedulerCurrentWeight > active[selected].SchedulerCurrentWeight ||
                    active[index].SchedulerCurrentWeight == active[selected].SchedulerCurrentWeight &&
                    active[index].ActivationOrder < active[selected].ActivationOrder) {
                    selected = index;
                }
            }

            active[selected].SchedulerCurrentWeight -= totalWeight;
            return selected;
        }

        private static double ResolveProgressIncrement(GeneratedBillOption option, int selectedQuality) {
            double m = Math.Clamp(selectedQuality, 1, 10);
            int qualityTarget = 0;
            for (int index = 0; index < option.Requirements.Count; index++) {
                BillRequirementSnapshot requirement = option.Requirements[index];
                if (requirement.Kind == BillRequirementKind.MinimumUnlockedDocumentQuality) {
                    qualityTarget = requirement.NumericTarget;
                    break;
                }
            }

            if (qualityTarget == 0) return m;
            double n = qualityTarget;
            if (m == n) return n / 2d;
            if (m < n) return m / (n - m);
            return n / 2d + (m - n);
        }

        private double ResolveBaseRewardStrength(
            GeneratedBillOption option,
            SignatureEvaluationResult result,
            BillEntries entries) {
            double requirementStrength = ResolveRequirementStrength(option, entries);

            double maximum = Math.Max(1d, entries.MaximumSignatureRewardMultiplier);
            double threshold = option.SignatureThreshold;
            double normalized = threshold >= 1f
                ? 0d
                : Math.Clamp((result.Similarity - threshold) / (1d - threshold), 0d, 1d);
            double signatureStrength = 1d + (maximum - 1d) * normalized;
            double strength = SaturatingMultiplyPositive(requirementStrength, signatureStrength);
            if (strength <= 0d) {
                throw new InvalidOperationException("Bill reward strength is outside the supported range.");
            }
            return strength;
        }

        private static double ResolveRequirementStrength(GeneratedBillOption option, BillEntries entries) {
            double requirementStrength = 1d;
            double requirementMultiplier = Math.Max(0d, entries.RequirementRewardFactorMultiplier);
            for (int index = 0; index < option.Requirements.Count; index++) {
                double contribution = SaturatingMultiplyPositive(
                    option.Requirements[index].Balance.RewardFactor,
                    requirementMultiplier);
                requirementStrength = SaturatingAddPositive(requirementStrength, contribution);
            }
            return requirementStrength;
        }

        private void ReconcileAfterExternalStateChange() {
            if (!_postInitialized || _isMutating || _rewards.Count == 0) {
                if (_postInitialized) NotifyChanged();
                return;
            }

            BillEntries entries = _billData.Value;
            StateView state = CreateStateView(_pending, _active, _completed);
            if (TryPrepareCatalog(state, entries, false, out CatalogBuild build) && build.Replaced) {
                _isMutating = true;
                try { ReplaceCatalog(build); }
                finally { _isMutating = false; }
            }
            NotifyChanged();
        }

        private void OnCacheInvalidated(Type type) {
            if (!_postInitialized || _isMutating || _handlingInvalidation) return;
            if (type != typeof(BillEntries) && type != typeof(DocumentEntries) &&
                type != typeof(GenerationEntries) && type != typeof(IncomeEntries)) return;

            _handlingInvalidation = true;
            try {
                if (type == typeof(BillEntries) || type == typeof(DocumentEntries)) {
                    ReconcileAfterExternalStateChange();
                }
                if (type == typeof(BillEntries)) {
                    InvalidateActiveCaches();
                    InvalidateAllCompletionGroups();
                }
                if (type == typeof(BillEntries) || type == typeof(GenerationEntries) ||
                    type == typeof(IncomeEntries)) {
                    MaterializePendingContributions(false);
                    RebuildAttributionShares();
                }
            }
            finally {
                _handlingInvalidation = false;
            }
        }

        private bool TryPrepareCatalog(
            StateView state,
            BillEntries entries,
            bool forceReroll,
            out CatalogBuild build) {
            build = default;
            if (ShouldSuppressCatalog(state, entries)) {
                build = CreateSuppressedCatalogBuild();
                return true;
            }
            if (_rewards.Count == 0) {
                build = new CatalogBuild(new List<GeneratedBillOption>(), _random.Fork(), _nextOptionId, true);
                return true;
            }

            int unlockedQuality = ResolveCandidateUnlockedQuality(state.Completed, entries);
            ExternalState external = CaptureExternalState(unlockedQuality);
            if (!forceReroll && IsCatalogValidForState(_catalog, state, entries, external)) {
                build = new CatalogBuild(_catalog, _random.Fork(), _nextOptionId, false);
                return true;
            }

            build = BuildFreshCatalog(state, entries, external);
            return build.Options.Count > 0;
        }

        private CatalogBuild BuildFreshCatalog(StateView state, BillEntries entries) {
            int unlockedQuality = ResolveCandidateUnlockedQuality(state.Completed, entries);
            return BuildFreshCatalog(state, entries, CaptureExternalState(unlockedQuality));
        }

        private CatalogBuild BuildFreshCatalog(
            StateView state,
            BillEntries entries,
            ExternalState external) {
            return BuildFreshCatalog(state, entries, external, _random, _nextOptionId);
        }

        private CatalogBuild BuildFreshCatalog(
            StateView state,
            BillEntries entries,
            ExternalState external,
            IBillRandom sourceRandom,
            long sourceNextOptionId) {
            IBillRandom random = sourceRandom.Fork();
            long nextOptionId = sourceNextOptionId;
            var eligible = new List<BillRewardDefinition>();
            foreach (BillRewardDefinition reward in _rewards.Values) {
                if (reward.Repeatable || !StateContainsReward(state, reward.Id)) eligible.Add(reward);
            }
            Shuffle(eligible, random);

            int targetCount = Math.Min(ResolveCatalogSize(entries), eligible.Count);
            var options = new List<GeneratedBillOption>(targetCount);
            var usedRewards = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < eligible.Count; index++) {
                BillRewardDefinition reward = eligible[index];
                if (TryBuildOption(reward, true, false, entries, external, random, nextOptionId,
                        out GeneratedBillOption option)) {
                    options.Add(option);
                    usedRewards.Add(reward.Id);
                    nextOptionId++;
                    break;
                }
            }

            if (options.Count == 0) {
                BillRewardDefinition fallback = null;
                for (int index = 0; index < eligible.Count; index++) {
                    if (eligible[index].Repeatable && eligible[index].MinimumRequirementCount == 0) {
                        fallback = eligible[index];
                        break;
                    }
                }
                if (fallback == null || !TryBuildOption(
                        fallback, true, true, entries, external, random, nextOptionId,
                        out GeneratedBillOption fallbackOption)) {
                    throw new InvalidOperationException("Bill fallback reward could not produce a valid option.");
                }
                options.Add(fallbackOption);
                usedRewards.Add(fallback.Id);
                nextOptionId++;
            }

            for (int index = 0; index < eligible.Count && options.Count < targetCount; index++) {
                BillRewardDefinition reward = eligible[index];
                if (!usedRewards.Add(reward.Id)) continue;
                if (!TryBuildOption(reward, false, false, entries, external, random, nextOptionId,
                        out GeneratedBillOption option)) continue;
                options.Add(option);
                nextOptionId++;
            }

            return new CatalogBuild(options, random, nextOptionId, true);
        }

        private bool TryBuildOption(
            BillRewardDefinition reward,
            bool forceSatisfied,
            bool forceZeroRequirements,
            BillEntries entries,
            ExternalState external,
            IBillRandom random,
            long optionId,
            out GeneratedBillOption option) {
            option = null;
            int count;
            if (forceZeroRequirements) count = 0;
            else {
                int minimum = reward.MinimumRequirementCount;
                int maximum = reward.MaximumRequirementCount;
                count = random.NextInt(minimum, maximum + 1);
            }

            var requirements = new List<BillRequirementSnapshot>(count);
            var usedKinds = new HashSet<BillRequirementKind>();
            for (int index = 0; index < count; index++) {
                bool desiredSatisfied = forceSatisfied || random.Chance(0.5d);
                if (!TryRollRequirement(desiredSatisfied, usedKinds, external, random,
                        out BillRequirementSnapshot requirement) &&
                    !TryRollRequirement(!desiredSatisfied, usedKinds, external, random, out requirement)) {
                    return false;
                }
                requirements.Add(requirement);
                usedKinds.Add(requirement.Kind);
            }

            Value rawCost = reward.BaseCost;
            double workFactor = 0d;
            double difficulty = 0d;
            for (int index = 0; index < requirements.Count; index++) {
                BillRequirementBalance balance = requirements[index].Balance;
                rawCost = MultiplyValueSafely(rawCost, balance.CostMultiplier);
                workFactor = SaturatingAddPositive(workFactor, balance.WorkFactor);
                difficulty = SaturatingAddPositive(difficulty, balance.DifficultyFactor);
            }

            double requiredProgress = SaturatingMultiplyPositive(
                reward.BaseRequiredProgress,
                SaturatingAddPositive(1d, workFactor));
            double threshold = Math.Clamp(
                entries.BaseSignatureThreshold +
                entries.MaximumThresholdAddition * Math.Clamp(difficulty, 0d, 1d),
                0d,
                1d);
            if (!IsFinite(requiredProgress) || requiredProgress <= 0d || !IsFinite(threshold)) return false;
            option = new GeneratedBillOption(
                optionId,
                reward,
                requirements,
                rawCost,
                requiredProgress,
                (float)threshold);
            return !forceSatisfied || AreRequirementsSatisfied(option, external);
        }

        private bool TryRollRequirement(
            bool satisfied,
            HashSet<BillRequirementKind> usedKinds,
            ExternalState external,
            IBillRandom random,
            out BillRequirementSnapshot result) {
            result = null;
            var candidates = new List<RequirementCandidate>();
            foreach (BillRequirementTemplateDefinition template in _templates.Values) {
                if (usedKinds.Contains(template.Kind)) continue;
                if (TryCreateCandidate(template, satisfied, external, out RequirementCandidate candidate)) {
                    candidates.Add(candidate);
                }
            }
            if (candidates.Count == 0) return false;

            RequirementCandidate selected = candidates[random.NextInt(0, candidates.Count)];
            int target = selected.MinimumTarget == selected.MaximumTarget
                ? selected.MinimumTarget
                : random.NextInt(selected.MinimumTarget, selected.MaximumTarget + 1);
            BillRequirementTemplateDefinition definition = selected.Template;
            result = new BillRequirementSnapshot(
                definition.Id,
                definition.Kind,
                target,
                definition.Kind == BillRequirementKind.OwnedUpgrade ? definition.UpgradeId : null,
                definition.ResolveBalance(target));
            return true;
        }

        private static bool TryCreateCandidate(
            BillRequirementTemplateDefinition template,
            bool satisfied,
            ExternalState external,
            out RequirementCandidate candidate) {
            candidate = default;
            if (template.Kind == BillRequirementKind.OwnedUpgrade) {
                bool owned = external.OwnedUpgradeIds.Contains(template.UpgradeId);
                if (owned != satisfied) return false;
                candidate = new RequirementCandidate(template, 0, 0);
                return true;
            }

            int minimum = template.MinimumTarget;
            int maximum = template.MaximumTarget;
            int current;
            if (template.Kind == BillRequirementKind.MinimumClerkCount) {
                current = external.ClerkCount;
            }
            else {
                minimum = Math.Max(minimum, 2);
                maximum = Math.Min(Math.Min(maximum, 10), external.UnlockedQuality + 1);
                current = external.UnlockedQuality;
            }

            if (satisfied) maximum = Math.Min(maximum, current);
            else minimum = Math.Max(minimum, current + 1);
            if (minimum > maximum) return false;
            candidate = new RequirementCandidate(template, minimum, maximum);
            return true;
        }

        private bool IsCatalogValidForState(
            IReadOnlyList<GeneratedBillOption> catalog,
            StateView state,
            BillEntries entries,
            ExternalState external) {
            int desired = Math.Min(ResolveCatalogSize(entries), CountEligibleRewards(state));
            if (catalog.Count != desired || catalog.Count == 0) return false;
            var rewardIds = new HashSet<string>(StringComparer.Ordinal);
            bool hasSatisfied = false;
            for (int index = 0; index < catalog.Count; index++) {
                GeneratedBillOption option = catalog[index];
                if (option?.Reward == null || !rewardIds.Add(option.Reward.Id)) return false;
                if (!option.Reward.Repeatable && StateContainsReward(state, option.Reward.Id)) return false;
                if (AreRequirementsSatisfied(option, external)) hasSatisfied = true;
            }
            return hasSatisfied;
        }

        private int ResolveCandidateUnlockedQuality(
            IReadOnlyList<BillCompletionRecord> completions,
            BillEntries entries) {
            DocumentEntries upgradeOnly = _documentCalculator.CalculateUpgradeOnly();
            DocumentEntries candidate = BillCompletionModifierEvaluator.Apply(upgradeOnly, completions, entries);
            return Math.Clamp(candidate.DocumentQualityLevel, 0, 9) + 1;
        }

        private ExternalState CaptureExternalState(int unlockedQuality) {
            var owned = new HashSet<string>(StringComparer.Ordinal);
            foreach (var state in _upgrades.OwnedUpgrades) owned.Add(state.Definition.Id);
            return new ExternalState(owned, _office.ClerkCount, Math.Clamp(unlockedQuality, 1, 10));
        }

        private static bool AreRequirementsSatisfied(GeneratedBillOption option, ExternalState external) {
            for (int index = 0; index < option.Requirements.Count; index++) {
                BillRequirementSnapshot requirement = option.Requirements[index];
                if (!IsRequirementSatisfied(requirement, external)) return false;
            }
            return true;
        }

        private static bool IsRequirementSatisfied(
            BillRequirementSnapshot requirement,
            ExternalState external) {
            return requirement.Kind switch {
                BillRequirementKind.OwnedUpgrade =>
                    external.OwnedUpgradeIds.Contains(requirement.UpgradeId),
                BillRequirementKind.MinimumClerkCount =>
                    external.ClerkCount >= requirement.NumericTarget,
                BillRequirementKind.MinimumUnlockedDocumentQuality =>
                    external.UnlockedQuality >= requirement.NumericTarget,
                _ => false
            };
        }

        private Value ResolvePrice(GeneratedBillOption option, BillEntries entries) {
            double multiplier = Math.Max(float.Epsilon, entries.CostMultiplier);
            return MultiplyValueSafely(option.RawCost, multiplier);
        }

        private void ReplaceCatalog(CatalogBuild build) {
            _catalog = build.Options;
            _random = build.Random;
            _nextOptionId = build.NextOptionId;
        }

        private CatalogBuild CreateSuppressedCatalogBuild() {
            return new CatalogBuild(new List<GeneratedBillOption>(), _random.Fork(), _nextOptionId, true);
        }

        private static bool ShouldSuppressCatalog(StateView state, BillEntries entries) {
            return state.Pending != null || state.Active.Count >= ResolveActiveLimit(entries);
        }

        private void ResetToDefaultState() {
            _pending = null;
            _active = new List<ActiveBillState>();
            _completed = new List<BillCompletionRecord>();
            _generationAttributionShares = Array.Empty<double>();
            _incomeAttributionShares = Array.Empty<double>();
            _generationAttributionShareSum = 0d;
            _incomeAttributionShareSum = 0d;
            _pendingGeneratedDocumentEquivalents = 0d;
            _pendingCreditedIncome = Value.Zero;
            _contributionDirty = false;
            _catalog = new List<GeneratedBillOption>();
            InvalidateClaims();
            _nextOptionId = 1L;
            _nextInstanceId = 1L;
            _nextActivationOrder = 1L;
            _nextCompletionOrder = 1L;
            if (_rewards.Count > 0) {
                ReplaceCatalog(BuildFreshCatalog(CreateStateView(null, _active, _completed), _billData.Value));
            }
            InvalidateActiveCaches();
            InvalidateAllCompletionGroups();
            RebuildAttributionShares();
        }

        private void InvalidateActiveCaches() {
            _cacheInvalidator.Invalidate(typeof(IncomeEntries));
            _cacheInvalidator.Invalidate(typeof(GenerationEntries));
        }

        private void InvalidateCompletionGroups(BillRewardDefinition reward) {
            ModifierDefinition[] definitions = reward.CompletionModifiers;
            if (definitions == null) return;
            var groups = new HashSet<Type>();
            for (int index = 0; index < definitions.Length; index++) {
                ModifierDefinition definition = definitions[index];
                if (definition?.NumericModifiers == null) continue;
                for (int modifierIndex = 0; modifierIndex < definition.NumericModifiers.Count; modifierIndex++) {
                    NumericModifierDefinition modifier = definition.NumericModifiers[modifierIndex];
                    if (modifier != null) groups.Add(modifier.GetGroupType());
                }
            }
            foreach (Type group in groups) if (group != typeof(BillEntries)) _cacheInvalidator.Invalidate(group);
        }

        private void InvalidateAllCompletionGroups() {
            var rewards = new HashSet<BillRewardDefinition>();
            for (int index = 0; index < _completed.Count; index++) rewards.Add(_completed[index].Reward);
            foreach (BillRewardDefinition reward in rewards) InvalidateCompletionGroups(reward);
        }

        private int CountEligibleRewards(StateView state) {
            int count = 0;
            foreach (BillRewardDefinition reward in _rewards.Values) {
                if (reward.Repeatable || !StateContainsReward(state, reward.Id)) count++;
            }
            return count;
        }

        private static bool StateContainsReward(StateView state, string rewardId) {
            if (state.Pending != null && !state.Pending.Option.Reward.Repeatable &&
                string.Equals(state.Pending.Option.Reward.Id, rewardId, StringComparison.Ordinal)) return true;
            for (int index = 0; index < state.Active.Count; index++) {
                BillRewardDefinition reward = state.Active[index].Option.Reward;
                if (!reward.Repeatable && string.Equals(reward.Id, rewardId, StringComparison.Ordinal)) return true;
            }
            for (int index = 0; index < state.Completed.Count; index++) {
                BillRewardDefinition reward = state.Completed[index].Reward;
                if (!reward.Repeatable && string.Equals(reward.Id, rewardId, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static void Shuffle<T>(List<T> values, IBillRandom random) {
            for (int index = values.Count - 1; index > 0; index--) {
                int other = random.NextInt(0, index + 1);
                (values[index], values[other]) = (values[other], values[index]);
            }
        }

        private GeneratedBillOption FindCatalogOption(long optionId) {
            for (int index = 0; index < _catalog.Count; index++) {
                if (_catalog[index].OptionId == optionId) return _catalog[index];
            }
            return null;
        }

        private ActiveBillState FindActive(long instanceId) {
            for (int index = 0; index < _active.Count; index++) {
                if (_active[index].InstanceId == instanceId) return _active[index];
            }
            return null;
        }

        private bool HasClaimForPending() {
            if (_pending == null) return false;
            foreach (long optionId in _activeClaims.Values) {
                if (optionId == _pending.Option.OptionId) return true;
            }
            return false;
        }

        private bool IsValidClaim(BillDocumentClaim claim) {
            return claim.Epoch == _claimEpoch &&
                   _activeClaims.TryGetValue(claim.Token, out long optionId) &&
                   optionId == claim.OptionId;
        }

        private void InvalidateClaims() {
            _claimEpoch++;
            _activeClaims.Clear();
        }

        private void PublishWalletChanged() {
            _ignoreWalletNotification = true;
            try { _wallet.NotifyBalanceChanged(); }
            finally { _ignoreWalletNotification = false; }
        }

        public IReadOnlyList<BillCompletionRecord> PrepareCompletionStatisticsSnapshot() {
            MaterializePendingContributions(false);
            return _completed;
        }

        internal void AccumulateGenerationWork(GenerationWork work) {
            if (!_postInitialized || work.DeltaPoints <= 0d || double.IsNaN(work.DeltaPoints) ||
                double.IsInfinity(work.DeltaPoints) || _generationAttributionShareSum <= 0d) return;
            double documentEquivalents = work.DeltaPoints / DocumentGeneratorService.PointsPerDocument;
            if (documentEquivalents <= 0d || double.IsNaN(documentEquivalents) ||
                double.IsInfinity(documentEquivalents)) return;
            _pendingGeneratedDocumentEquivalents = SaturatingAddPositive(
                _pendingGeneratedDocumentEquivalents,
                documentEquivalents);
            MarkContributionDirty();
        }

        internal void AccumulateMoneyTransaction(MoneyTransaction transaction) {
            if (!_postInitialized || transaction.Credited.IsZero || _incomeAttributionShareSum <= 0d) return;
            _pendingCreditedIncome += transaction.Credited;
            MarkContributionDirty();
        }

        private void OnUpdate(float deltaTime) {
            if (deltaTime > 0f && !float.IsNaN(deltaTime) && !float.IsInfinity(deltaTime)) {
                for (int index = 0; index < _active.Count; index++) {
                    ActiveBillState active = _active[index];
                    active.ElapsedWorkSeconds = SaturatingAddPositive(active.ElapsedWorkSeconds, deltaTime);
                }
            }

            if (!_contributionDirty) return;
            _contributionNotificationDelay += Math.Max(0f, deltaTime);
            if (_contributionNotificationDelay < 1f) return;
            MaterializePendingContributions(false);
            NotifyChanged();
        }

        private void MarkContributionDirty() {
            if (_pendingGeneratedDocumentEquivalents <= 0d && _pendingCreditedIncome.IsZero) return;
            _contributionDirty = true;
        }

        private void MaterializePendingContributions(bool notify) {
            if (_completed.Count > 0) {
                if (_pendingGeneratedDocumentEquivalents > 0d) {
                    int count = Math.Min(_completed.Count, _generationAttributionShares.Length);
                    for (int index = 0; index < count; index++) {
                        double share = _generationAttributionShares[index];
                        if (share <= 0d) continue;
                        double attributed = SaturatingMultiplyPositive(
                            _pendingGeneratedDocumentEquivalents,
                            share);
                        _completed[index].AdditionalGeneratedDocuments = SaturatingAddPositive(
                            _completed[index].AdditionalGeneratedDocuments,
                            attributed);
                    }
                }

                if (!_pendingCreditedIncome.IsZero) {
                    int count = Math.Min(_completed.Count, _incomeAttributionShares.Length);
                    for (int index = 0; index < count; index++) {
                        double share = _incomeAttributionShares[index];
                        if (share <= 0d) continue;
                        _completed[index].AdditionalIncome += MultiplyValueSafely(_pendingCreditedIncome, share);
                    }
                }
            }

            bool changed = _pendingGeneratedDocumentEquivalents > 0d || !_pendingCreditedIncome.IsZero;
            _pendingGeneratedDocumentEquivalents = 0d;
            _pendingCreditedIncome = Value.Zero;
            _contributionDirty = false;
            _contributionNotificationDelay = 0f;
            if (notify && changed) NotifyChanged();
        }

        private void RebuildAttributionShares() {
            _generationAttributionShares = BuildGenerationAttributionShares();
            _incomeAttributionShares = BuildIncomeAttributionShares();
            _generationAttributionShareSum = SumShares(_generationAttributionShares);
            _incomeAttributionShareSum = SumShares(_incomeAttributionShares);
        }

        private double[] BuildGenerationAttributionShares() {
            var shares = new double[_completed.Count];
            if (_completed.Count == 0 || _generationCalculator == null || !_postInitialized) return shares;

            GenerationEntries current = _generationCalculator.CalculateUpgradeOnly();
            var marginals = new double[_completed.Count];
            double positiveTotal = 0d;
            for (int index = 0; index < _completed.Count; index++) {
                GenerationEntries next = BillCompletionModifierEvaluator.ApplySingle(
                    current,
                    _completed[index],
                    _billData.Value);
                double marginal = Math.Max(0d, (double)next.TokenPerSecond - current.TokenPerSecond);
                if (!double.IsNaN(marginal) && !double.IsInfinity(marginal)) {
                    marginals[index] = marginal;
                    positiveTotal = SaturatingAddPositive(positiveTotal, marginal);
                }
                current = next;
            }

            double denominator = Math.Max(positiveTotal, Math.Max(0d, current.TokenPerSecond));
            if (denominator <= 0d || double.IsInfinity(denominator)) return shares;
            for (int index = 0; index < shares.Length; index++) shares[index] = marginals[index] / denominator;
            return shares;
        }

        private double[] BuildIncomeAttributionShares() {
            var shares = new double[_completed.Count];
            if (_completed.Count == 0 || _incomeCalculator == null || !_postInitialized) return shares;

            IncomeEntries current = _incomeCalculator.CalculateUpgradeOnly();
            var marginals = new Value[_completed.Count];
            Value positiveTotal = Value.Zero;
            for (int index = 0; index < _completed.Count; index++) {
                IncomeEntries next = BillCompletionModifierEvaluator.ApplySingle(
                    current,
                    _completed[index],
                    _billData.Value);
                if (next.IncomePerDocument > current.IncomePerDocument) {
                    Value marginal = (next.IncomePerDocument - current.IncomePerDocument).Value;
                    marginals[index] = marginal;
                    positiveTotal += marginal;
                }
                current = next;
            }

            Value denominator = positiveTotal > current.IncomePerDocument
                ? positiveTotal
                : current.IncomePerDocument;
            if (denominator.IsZero) return shares;
            for (int index = 0; index < shares.Length; index++) {
                shares[index] = ResolveValueRatio(marginals[index], denominator);
            }
            return shares;
        }

        private static double ResolveValueRatio(Value numerator, Value denominator) {
            if (numerator.IsZero || denominator.IsZero || numerator > denominator) return numerator > denominator ? 1d : 0d;
            double logarithm = numerator.ToLog10() - denominator.ToLog10();
            if (double.IsNaN(logarithm)) return 0d;
            if (logarithm >= 0d) return 1d;
            if (logarithm < -324d) return 0d;
            return Math.Clamp(Math.Pow(10d, logarithm), 0d, 1d);
        }

        private static double SumShares(double[] shares) {
            double result = 0d;
            for (int index = 0; index < shares.Length; index++) result += Math.Max(0d, shares[index]);
            return Math.Min(1d, result);
        }

        private void NotifyChanged() => _changed.OnNext(Unit.Default);
        private void NotifyDocumentOffersChanged() => _documentOffersChanged.OnNext(Unit.Default);

        private static List<ActiveBillState> CloneActive(IReadOnlyList<ActiveBillState> source) {
            var result = new List<ActiveBillState>(source.Count);
            for (int index = 0; index < source.Count; index++) result.Add(source[index].Clone());
            return result;
        }

        private static StateView CreateStateView(
            PendingBillState pending,
            IReadOnlyList<ActiveBillState> active,
            IReadOnlyList<BillCompletionRecord> completed) {
            return new StateView(pending, active, completed);
        }

        private static int ResolveCatalogSize(BillEntries entries) => Math.Clamp(entries.CatalogSize, 1, 64);
        private static int ResolveActiveLimit(BillEntries entries) => Math.Clamp(entries.ActiveProjectLimit, 1, 64);
        private static int ResolveMaximumPriorityWeight(BillEntries entries) =>
            Math.Max(1, entries.MaximumPriorityWeight);

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static double SaturatingAddPositive(double left, double right) {
            if (double.IsNaN(left) || left <= 0d) left = 0d;
            if (double.IsNaN(right) || right <= 0d) right = 0d;
            if (double.IsPositiveInfinity(left) || double.IsPositiveInfinity(right) ||
                left > double.MaxValue - right) return double.MaxValue;
            return left + right;
        }

        private static double SaturatingMultiplyPositive(double left, double right) {
            if (double.IsNaN(left) || double.IsNaN(right) || left <= 0d || right <= 0d) return 0d;
            if (double.IsPositiveInfinity(left) || double.IsPositiveInfinity(right) ||
                left > double.MaxValue / right) return double.MaxValue;
            return left * right;
        }

        private static Value MultiplyValueSafely(Value value, double multiplier) {
            if (value.IsZero || double.IsNaN(multiplier) || multiplier <= 0d) return Value.Zero;
            if (double.IsPositiveInfinity(multiplier)) return Value.Infinity;
            double logarithm = value.ToLog10() + Math.Log10(multiplier);
            if (double.IsNaN(logarithm)) return Value.Zero;
            if (double.IsPositiveInfinity(logarithm) || logarithm >= MaximumValueLog10) {
                return Value.Infinity;
            }
            return Value.FromLog10(logarithm);
        }

        private static void ValidateReward(BillRewardDefinition reward) {
            if (reward == null) throw new InvalidOperationException("Bill catalog contains a null reward.");
            if (string.IsNullOrWhiteSpace(reward.Id)) {
                throw new InvalidOperationException($"Bill reward '{reward.name}' has an empty ID.");
            }
            if (reward.BaseCost.IsZero || !IsFinite(reward.BaseRequiredProgress) || reward.BaseRequiredProgress <= 0d ||
                reward.MinimumRequirementCount < 0 || reward.MaximumRequirementCount < reward.MinimumRequirementCount ||
                reward.MaximumRequirementCount > Enum.GetValues(typeof(BillRequirementKind)).Length ||
                !IsFinite(reward.BaseActiveGenerationBonus) || reward.BaseActiveGenerationBonus < 0d) {
                throw new InvalidOperationException($"Bill reward '{reward.Id}' contains invalid generation values.");
            }

            ModifierDefinition[] definitions = reward.CompletionModifiers;
            if (definitions == null) return;
            var modifierIds = new HashSet<string>(StringComparer.Ordinal);
            for (int definitionIndex = 0; definitionIndex < definitions.Length; definitionIndex++) {
                ModifierDefinition definition = definitions[definitionIndex];
                if (definition?.NumericModifiers == null) continue;
                for (int modifierIndex = 0; modifierIndex < definition.NumericModifiers.Count; modifierIndex++) {
                    NumericModifierDefinition modifier = definition.NumericModifiers[modifierIndex];
                    if (modifier == null) continue;
                    if (string.IsNullOrWhiteSpace(modifier.Id) || !modifierIds.Add(modifier.Id)) {
                        throw new InvalidOperationException(
                            $"Bill reward '{reward.Id}' has a missing or duplicate numeric modifier ID.");
                    }
                    if (modifier.GetGroupType() == typeof(BillEntries)) {
                        throw new InvalidOperationException(
                            $"Bill reward '{reward.Id}' cannot modify BillEntries.");
                    }
                    if (modifier.Operation == NumericModifierOperation.Override) {
                        throw new InvalidOperationException(
                            $"Bill reward '{reward.Id}' cannot use Override completion modifiers.");
                    }
                }
            }
        }

        private static void ValidateTemplate(BillRequirementTemplateDefinition template) {
            if (template == null) throw new InvalidOperationException("Bill catalog contains a null requirement template.");
            if (string.IsNullOrWhiteSpace(template.Id)) {
                throw new InvalidOperationException($"Bill requirement template '{template.name}' has an empty ID.");
            }
            if (!Enum.IsDefined(typeof(BillRequirementKind), template.Kind)) {
                throw new InvalidOperationException($"Bill requirement '{template.Id}' has an invalid kind.");
            }
            if (template.Kind == BillRequirementKind.OwnedUpgrade) {
                if (string.IsNullOrWhiteSpace(template.UpgradeId)) {
                    throw new InvalidOperationException($"Bill requirement '{template.Id}' has no upgrade ID.");
                }
            }
            else if (template.MinimumTarget < 0 || template.MaximumTarget < template.MinimumTarget) {
                throw new InvalidOperationException($"Bill requirement '{template.Id}' has an invalid target range.");
            }
            ValidateBalance(template.Id, template.MinimumBalance);
            ValidateBalance(template.Id, template.MaximumBalance);
        }

        private static void ValidateBalance(string id, BillRequirementBalance balance) {
            if (!IsFinite(balance.CostMultiplier) || balance.CostMultiplier < 1d ||
                !IsFinite(balance.WorkFactor) || balance.WorkFactor < 0d ||
                !IsFinite(balance.RewardFactor) || balance.RewardFactor < 0d ||
                !IsFinite(balance.DifficultyFactor) || balance.DifficultyFactor < 0d) {
                throw new InvalidOperationException($"Bill requirement '{id}' has invalid balance values.");
            }
        }

        public void Dispose() {
            _subscriptions.Dispose();
            _changed.Dispose();
            _documentOffersChanged.Dispose();
            InvalidateClaims();
            _catalogLease?.Dispose();
            _catalogLease = null;
            _catalog.Clear();
            _active.Clear();
            _completed.Clear();
            _pending = null;
            _deferredRestore = null;
            _requirementPresentation.Clear();
            _generationAttributionShares = Array.Empty<double>();
            _incomeAttributionShares = Array.Empty<double>();
            _generationAttributionShareSum = 0d;
            _incomeAttributionShareSum = 0d;
            _pendingGeneratedDocumentEquivalents = 0d;
            _pendingCreditedIncome = Value.Zero;
        }

        internal readonly struct BillDocumentClaim {
            internal int Epoch { get; }
            internal long Token { get; }
            internal long OptionId { get; }
            internal float SignatureThreshold { get; }

            internal BillDocumentClaim(
                int epoch,
                long token,
                long optionId,
                float signatureThreshold) {
                Epoch = epoch;
                Token = token;
                OptionId = optionId;
                SignatureThreshold = signatureThreshold;
            }
        }

        private readonly struct RequirementCandidate {
            public BillRequirementTemplateDefinition Template { get; }
            public int MinimumTarget { get; }
            public int MaximumTarget { get; }

            public RequirementCandidate(
                BillRequirementTemplateDefinition template,
                int minimumTarget,
                int maximumTarget) {
                Template = template;
                MinimumTarget = minimumTarget;
                MaximumTarget = maximumTarget;
            }
        }

        private readonly struct ExternalState {
            public HashSet<string> OwnedUpgradeIds { get; }
            public int ClerkCount { get; }
            public int UnlockedQuality { get; }

            public ExternalState(HashSet<string> ownedUpgradeIds, int clerkCount, int unlockedQuality) {
                OwnedUpgradeIds = ownedUpgradeIds;
                ClerkCount = clerkCount;
                UnlockedQuality = unlockedQuality;
            }
        }

        private readonly struct StateView {
            public PendingBillState Pending { get; }
            public IReadOnlyList<ActiveBillState> Active { get; }
            public IReadOnlyList<BillCompletionRecord> Completed { get; }

            public StateView(
                PendingBillState pending,
                IReadOnlyList<ActiveBillState> active,
                IReadOnlyList<BillCompletionRecord> completed) {
                Pending = pending;
                Active = active;
                Completed = completed;
            }
        }

        private readonly struct CatalogBuild {
            public List<GeneratedBillOption> Options { get; }
            public IBillRandom Random { get; }
            public long NextOptionId { get; }
            public bool Replaced { get; }

            public CatalogBuild(
                List<GeneratedBillOption> options,
                IBillRandom random,
                long nextOptionId,
                bool replaced) {
                Options = options;
                Random = random;
                NextOptionId = nextOptionId;
                Replaced = replaced;
            }
        }
    }
}
