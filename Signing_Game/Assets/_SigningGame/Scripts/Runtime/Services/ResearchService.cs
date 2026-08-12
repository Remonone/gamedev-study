using System;
using System.Collections.Generic;
using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Enums;
using Data.Modifiers;
using Data.Research;
using Data.Results;
using R3;
using Services.Locator;
using UnityEngine;
using Utils;

namespace Services {
    public sealed partial class ResearchService : IService, IInitialize, IPostInitialize, ISaveable {
        internal const double MinimumRequiredPoints = 1d;

        private readonly Subject<Unit> _changed = new();
        private readonly Subject<Unit> _documentOffersChanged = new();
        private readonly CompositeDisposable _subscriptions = new();
        private readonly Dictionary<long, string> _activeClaims = new();
        private readonly List<PracticeDefinition> _offers = new();
        private readonly List<ActivePracticeState> _active = new();

        private Dictionary<string, PracticeDefinition> _practices = new(StringComparer.Ordinal);
        private Dictionary<string, PracticeRarityDefinition> _rarities = new(StringComparer.Ordinal);
        private IAssetProvider _assetProvider;
        private IAssetListLease<ResearchCatalogDefinition> _catalogLease;
        private IResearchRandom _random;
        private Observable<float> _updates;
        private AcceptedNormalDocumentService _acceptedDocuments;
        private UnlockService _unlocks;
        private WalletService _wallet;
        private IReadOnlyCacheData<ResearchEntries> _researchData;
        private ICacheInvalidator _cacheInvalidator;
        private CacheVersionService _cacheVersions;
        private PendingPracticeState? _pending;
        private RestoreData _deferredRestore;
        private double _progress;
        private long _resolvedCycles;
        private bool _postInitialized;
        private bool _lastUnlocked;
        private bool _isMutating;
        private bool _isRestoring;
        private bool _isReconcilingCache;
        private int _claimEpoch;
        private long _nextClaimToken;

        public string SaveId => "Research";
        public Observable<Unit> Changed => _changed;
        public Observable<Unit> DocumentOffersChanged => _documentOffersChanged;
        public IReadOnlyList<PracticeDefinition> CurrentOffers => _offers;
        public IReadOnlyList<ActivePracticeState> ActivePractices => _active;
        public PendingPracticeState? Pending => _pending;
        public double Progress => _progress;
        public long ResolvedCycles => _resolvedCycles;
        public bool IsUnlocked => _unlocks?.IsUnlocked(FeatureIds.Archive) ?? false;
        public bool HasConfiguredPractices => _practices.Count > 0;
        public double RequiredPoints => _postInitialized ? ResolveRequiredPoints(_researchData.Value, _resolvedCycles) : MinimumRequiredPoints;
        public Value SalePayout => CalculateSalePayout(_offers);
        internal ulong RandomState => _random.State;

        public bool TryGetRarityPresentation(string rarityId, out string displayName, out Color color) {
            if (!string.IsNullOrWhiteSpace(rarityId) && _rarities.TryGetValue(rarityId, out PracticeRarityDefinition rarity)) {
                displayName = rarity.DisplayName;
                color = rarity.Color;
                return true;
            }
            displayName = string.Empty;
            color = Color.white;
            return false;
        }

        public ResearchService() : this(null, null, default) { }

        internal ResearchService(
            IAssetProvider assetProvider,
            IResearchRandom random,
            Observable<float> updates = default) {
            _assetProvider = assetProvider;
            _random = random ?? new ResearchRandom(unchecked((ulong)DateTime.UtcNow.Ticks));
            _updates = updates;
        }

        public async UniTask InitializeAsync(IServiceScope scope) {
            _assetProvider ??= scope.Container.Get<IAssetProvider>();
            _catalogLease = await _assetProvider.LoadAssetsByLabelAsync<ResearchCatalogDefinition>(
                AddressableConstants.RESEARCH_CATALOG_LABEL);
            if (_catalogLease.Assets.Count != 1) {
                throw new InvalidOperationException(
                    $"Exactly one ResearchCatalogDefinition is required, found {_catalogLease.Assets.Count}.");
            }
            BuildDefinitions(_catalogLease.Assets[0]);
        }

        public UniTask PostInitializeAsync(IServiceScope scope) {
            _acceptedDocuments = scope.Get<AcceptedNormalDocumentService>();
            _unlocks = scope.Get<UnlockService>();
            _wallet = scope.Get<WalletService>();
            _researchData = scope.Get<PlayerStatStash>().ResearchData;
            _cacheInvalidator = scope.Get<ICacheInvalidator>();
            _cacheVersions = scope.Get<CacheVersionService>();
            _postInitialized = true;
            _lastUnlocked = IsUnlocked;

            if (_deferredRestore != null) {
                RestoreData restore = _deferredRestore;
                _deferredRestore = null;
                try { ApplyRestore(restore); }
                catch (Exception exception) {
                    Debug.LogWarning($"Failed to apply the deferred Research save section. Default state will be used.\n{exception}");
                    ResetRuntimeState();
                }
            }

            _acceptedDocuments.Processed.Subscribe(ProcessAcceptedDocument).AddTo(_subscriptions);
            _unlocks.Changed.Subscribe(_ => OnUnlocksChanged()).AddTo(_subscriptions);
            _cacheVersions.Invalidated.Subscribe(OnCacheInvalidated).AddTo(_subscriptions);
            Observable<float> updates = _updates ?? Observable.EveryUpdate().Select(_ => Time.deltaTime);
            updates.Subscribe(Tick).AddTo(_subscriptions);
            ReconcileProgress();
            return UniTask.CompletedTask;
        }

        internal void BuildDefinitions(ResearchCatalogDefinition catalog) {
            if (catalog == null) throw new InvalidOperationException("Research catalog asset is null.");
            var rarities = new Dictionary<string, PracticeRarityDefinition>(StringComparer.Ordinal);
            long totalWeight = 0L;
            PracticeRarityDefinition[] rarityDefinitions = catalog.Rarities ?? Array.Empty<PracticeRarityDefinition>();
            for (int index = 0; index < rarityDefinitions.Length; index++) {
                PracticeRarityDefinition rarity = rarityDefinitions[index];
                if (string.IsNullOrWhiteSpace(rarity.Id)) throw new InvalidOperationException("Research rarity requires an ID.");
                if (rarity.SelectionWeight <= 0) throw new InvalidOperationException($"Research rarity '{rarity.Id}' requires a positive selection weight.");
                if (rarity.SalePrice.Stored < 0d || rarity.SalePrice.Base.Degree < 0 ||
                    double.IsNaN(rarity.SalePrice.Stored) || double.IsInfinity(rarity.SalePrice.Stored)) {
                    throw new InvalidOperationException($"Research rarity '{rarity.Id}' has an invalid sale price.");
                }
                totalWeight += rarity.SelectionWeight;
                if (totalWeight > int.MaxValue) throw new InvalidOperationException("Research rarity weights exceed Int32 range.");
                if (!rarities.TryAdd(rarity.Id, rarity)) throw new InvalidOperationException($"Duplicate research rarity ID '{rarity.Id}'.");
            }

            var practices = new Dictionary<string, PracticeDefinition>(StringComparer.Ordinal);
            PracticeDefinition[] definitions = catalog.Practices ?? Array.Empty<PracticeDefinition>();
            for (int index = 0; index < definitions.Length; index++) {
                PracticeDefinition definition = definitions[index];
                ValidatePracticeDefinition(definition, rarities);
                if (!practices.TryAdd(definition.Id, definition)) {
                    throw new InvalidOperationException($"Duplicate practice ID '{definition.Id}'.");
                }
            }
            _rarities = rarities;
            _practices = practices;
        }

        public bool TrySelectPractice(string practiceId) {
            if (!IsUnlocked || _isMutating || _isRestoring || _pending.HasValue || string.IsNullOrWhiteSpace(practiceId)) return false;
            PracticeDefinition selected = null;
            for (int index = 0; index < _offers.Count; index++) {
                if (string.Equals(_offers[index].Id, practiceId, StringComparison.Ordinal)) {
                    selected = _offers[index];
                    break;
                }
            }
            if (selected == null) return false;

            _isMutating = true;
            try {
                _offers.Clear();
                _pending = new PendingPracticeState(selected.Id, selected.SignatureThreshold);
                InvalidateClaims();
            }
            finally { _isMutating = false; }
            NotifyChanged();
            NotifyDocumentOffersChanged();
            return true;
        }

        public bool TrySellOffer() {
            if (!IsUnlocked || _isMutating || _isRestoring || _pending.HasValue || _offers.Count == 0) return false;
            Value payout = CalculateSalePayout(_offers);
            bool walletChanged;
            _isMutating = true;
            try {
                _offers.Clear();
                _progress = 0d;
                if (_resolvedCycles < long.MaxValue) _resolvedCycles++;
                walletChanged = _wallet.ReplenishWallet(payout, false);
                InvalidateClaims();
            }
            finally { _isMutating = false; }
            if (walletChanged) _wallet.NotifyBalanceChanged();
            NotifyChanged();
            NotifyDocumentOffersChanged();
            return true;
        }

        internal bool TryPeekPendingDocument(out Data.Documents.DocumentOffer offer) {
            offer = null;
            if (!IsUnlocked || !_pending.HasValue || HasClaimForPending()) return false;
            PendingPracticeState pending = _pending.Value;
            if (!_practices.TryGetValue(pending.PracticeId, out PracticeDefinition definition)) return false;
            offer = new Data.Documents.DocumentOffer(
                new Data.Documents.DocumentOfferKey(Data.Documents.DocumentKind.Practice, definition.Id),
                true,
                definition.DisplayName,
                definition.Icon);
            return true;
        }

        internal bool TryClaimPending(string practiceId, out PracticeDocumentClaim claim) {
            claim = default;
            if (!IsUnlocked || _isMutating || _isRestoring || !_pending.HasValue || HasClaimForPending()) return false;
            PendingPracticeState pending = _pending.Value;
            if (!string.Equals(pending.PracticeId, practiceId, StringComparison.Ordinal)) return false;
            long token = ++_nextClaimToken;
            _activeClaims.Add(token, practiceId);
            claim = new PracticeDocumentClaim(_claimEpoch, token, practiceId, pending.FrozenSignatureThreshold);
            NotifyDocumentOffersChanged();
            return true;
        }

        internal bool TryReleaseClaim(PracticeDocumentClaim claim) {
            if (!IsValidClaim(claim)) return false;
            _activeClaims.Remove(claim.Token);
            NotifyDocumentOffersChanged();
            return true;
        }

        internal bool TryProcessClaim(PracticeDocumentClaim claim, SignatureEvaluationResult result) {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (_isMutating || _isRestoring || !IsUnlocked || !IsValidClaim(claim) || !_pending.HasValue ||
                !string.Equals(_pending.Value.PracticeId, claim.PracticeId, StringComparison.Ordinal) ||
                !_practices.TryGetValue(claim.PracticeId, out PracticeDefinition definition)) return false;

            bool accepted = result.Status == SignatureEvaluationStatus.Accepted;
            bool invalid = result.Status == SignatureEvaluationStatus.InvalidAttempt ||
                           float.IsNaN(result.Similarity) || float.IsInfinity(result.Similarity);
            double ratio = invalid ? 0d : Math.Max(0d, result.Similarity) / claim.SignatureThreshold;
            if (double.IsNaN(ratio) || double.IsInfinity(ratio)) ratio = 0d;
            double instantMultiplier = accepted ? ratio : Math.Clamp(ratio, 0d, 0.999999999d);
            float modifierEffectiveness = accepted ? SaturatingFloat(ratio) : 1f;
            ActivePracticeState newActive = null;
            Value payout = Value.Zero;
            if (definition.EffectKind == PracticeEffectKind.NumericModifiers) {
                newActive = new ActivePracticeState(
                    definition,
                    modifierEffectiveness,
                    accepted,
                    accepted ? 0d : definition.FailedSignatureDurationSeconds);
            }
            else payout = MultiplyValueSafely(definition.InstantMoney, instantMultiplier);

            _isMutating = true;
            bool walletChanged;
            try {
                _pending = null;
                _activeClaims.Remove(claim.Token);
                _progress = 0d;
                if (_resolvedCycles < long.MaxValue) _resolvedCycles++;
                if (newActive != null) _active.Add(newActive);
                walletChanged = _wallet.ReplenishWallet(payout, false);
                if (newActive != null) InvalidatePracticeGroups(newActive.Definition);
            }
            finally { _isMutating = false; }
            if (walletChanged) _wallet.NotifyBalanceChanged();
            NotifyChanged();
            NotifyDocumentOffersChanged();
            return true;
        }

        internal void Tick(float deltaTime) {
            if (!IsUnlocked || _active.Count == 0 || deltaTime <= 0f ||
                float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            bool displayChanged = false;
            bool expired = false;
            for (int index = _active.Count - 1; index >= 0; index--) {
                ActivePracticeState state = _active[index];
                if (state.IsPermanent) continue;
                int beforeBucket = RemainingSecondBucket(state.RemainingSeconds);
                state.RemainingSeconds = Math.Max(0d, state.RemainingSeconds - deltaTime);
                int afterBucket = RemainingSecondBucket(state.RemainingSeconds);
                displayChanged |= beforeBucket != afterBucket;
                if (state.RemainingSeconds > 0d) continue;
                _active.RemoveAt(index);
                InvalidatePracticeGroups(state.Definition);
                expired = true;
            }
            if (expired) ReconcileProgress();
            if (displayChanged || expired) NotifyChanged();
        }

        public void Dispose() {
            _subscriptions.Dispose();
            _catalogLease?.Dispose();
            _catalogLease = null;
            _changed.Dispose();
            _documentOffersChanged.Dispose();
            _offers.Clear();
            _active.Clear();
            _activeClaims.Clear();
            _practices.Clear();
            _rarities.Clear();
        }

        private void ProcessAcceptedDocument(AcceptedNormalDocument document) {
            if (!IsUnlocked || _isMutating || _isRestoring || _offers.Count > 0 || _pending.HasValue) return;
            ResearchEntries entries = _researchData.Value;
            double required = ResolveRequiredPoints(entries, _resolvedCycles);
            if (_progress >= required) {
                _progress = required;
                ReconcileProgress();
                return;
            }
            double points = Math.Max(0d, FiniteOrZero(entries.PointsPerAcceptedDocument));
            if (points <= 0d) return;
            if (document.ProcessingQuality >= Math.Clamp(entries.DoublePointQualityThreshold, 0f, 1f) &&
                _random.Chance(Math.Clamp(entries.DoublePointChance, 0f, 1f))) {
                points = SaturatingAdd(points, points);
            }
            _progress = Math.Min(required, SaturatingAdd(_progress, points));
            ReconcileProgress();
            NotifyChanged();
        }

        private void OnUnlocksChanged() {
            bool unlocked = IsUnlocked;
            if (unlocked == _lastUnlocked) return;
            bool becameUnlocked = !_lastUnlocked && unlocked;
            _lastUnlocked = unlocked;
            InvalidateGroups(CollectAffectedGroups(_active));
            if (becameUnlocked) ReconcileProgress();
            NotifyChanged();
            NotifyDocumentOffersChanged();
        }

        private void OnCacheInvalidated(Type type) {
            if (type != typeof(ResearchEntries) || !_postInitialized || _isReconcilingCache ||
                _isRestoring || _isMutating) return;
            _isReconcilingCache = true;
            try {
                if (_offers.Count == 0 && !_pending.HasValue) {
                    _progress = Math.Min(_progress, RequiredPoints);
                    ReconcileProgress();
                    NotifyChanged();
                }
            }
            finally { _isReconcilingCache = false; }
        }

        private void ReconcileProgress() {
            if (!_postInitialized || !IsUnlocked || _isMutating || _isRestoring || _offers.Count > 0 || _pending.HasValue) return;
            double required = RequiredPoints;
            if (_progress < required) return;
            _progress = required;
            if (!HasAnyEligiblePractice()) return;
            List<PracticeDefinition> generated = GenerateOffers(out IResearchRandom nextRandom);
            if (generated.Count == 0) return;
            _offers.AddRange(generated);
            _random = nextRandom;
            NotifyDocumentOffersChanged();
        }

        private bool HasAnyEligiblePractice() {
            foreach (PracticeDefinition definition in _practices.Values) {
                if (IsEligible(definition)) return true;
            }
            return false;
        }

        private List<PracticeDefinition> GenerateOffers(out IResearchRandom nextRandom) {
            nextRandom = _random;
            var candidatesByRarity = new Dictionary<string, List<PracticeDefinition>>(StringComparer.Ordinal);
            foreach (PracticeDefinition definition in _practices.Values) {
                if (!IsEligible(definition)) continue;
                if (!candidatesByRarity.TryGetValue(definition.RarityId, out List<PracticeDefinition> list)) {
                    list = new List<PracticeDefinition>();
                    candidatesByRarity.Add(definition.RarityId, list);
                }
                list.Add(definition);
            }
            if (candidatesByRarity.Count == 0) return new List<PracticeDefinition>();
            foreach (List<PracticeDefinition> list in candidatesByRarity.Values) {
                list.Sort((left, right) => string.Compare(left.Id, right.Id, StringComparison.Ordinal));
            }

            IResearchRandom random = _random.Fork();
            int target = Math.Clamp(_researchData.Value.OfferCount, 1, 64);
            var result = new List<PracticeDefinition>(target);
            for (int draw = 0; draw < target && candidatesByRarity.Count > 0; draw++) {
                var rarityIds = new List<string>(candidatesByRarity.Keys);
                rarityIds.Sort(StringComparer.Ordinal);
                int totalWeight = 0;
                for (int index = 0; index < rarityIds.Count; index++) totalWeight += _rarities[rarityIds[index]].SelectionWeight;
                int roll = random.NextInt(0, totalWeight);
                string selectedRarity = rarityIds[0];
                for (int index = 0; index < rarityIds.Count; index++) {
                    int weight = _rarities[rarityIds[index]].SelectionWeight;
                    if (roll < weight) { selectedRarity = rarityIds[index]; break; }
                    roll -= weight;
                }
                List<PracticeDefinition> practices = candidatesByRarity[selectedRarity];
                int selectedIndex = random.NextInt(0, practices.Count);
                result.Add(practices[selectedIndex]);
                practices.RemoveAt(selectedIndex);
                if (practices.Count == 0) candidatesByRarity.Remove(selectedRarity);
            }
            nextRandom = random;
            return result;
        }

        private bool IsEligible(PracticeDefinition definition) {
            if (definition == null) return false;
            if (definition.EffectKind == PracticeEffectKind.InstantMoney) return true;
            for (int index = 0; index < _active.Count; index++) {
                if (string.Equals(_active[index].Definition.Id, definition.Id, StringComparison.Ordinal)) return false;
            }
            return true;
        }

        private Value CalculateSalePayout(IReadOnlyList<PracticeDefinition> offers) {
            if (offers == null || offers.Count == 0) return Value.Zero;
            Value sum = Value.Zero;
            for (int index = 0; index < offers.Count; index++) {
                if (_rarities.TryGetValue(offers[index].RarityId, out PracticeRarityDefinition rarity)) sum += rarity.SalePrice;
            }
            return sum / offers.Count;
        }

        private void InvalidatePracticeGroups(PracticeDefinition practice) {
            if (_cacheInvalidator == null || practice?.Modifiers == null) return;
            var groups = new HashSet<Type>();
            for (int index = 0; index < practice.Modifiers.Length; index++) {
                ModifierDefinition definition = practice.Modifiers[index];
                if (definition?.NumericModifiers == null) continue;
                for (int modifierIndex = 0; modifierIndex < definition.NumericModifiers.Count; modifierIndex++) {
                    NumericModifierDefinition modifier = definition.NumericModifiers[modifierIndex];
                    if (modifier != null) groups.Add(modifier.GetGroupType());
                }
            }
            foreach (Type group in groups) _cacheInvalidator.Invalidate(group);
        }

        private static void ValidatePracticeDefinition(
            PracticeDefinition definition,
            IReadOnlyDictionary<string, PracticeRarityDefinition> rarities) {
            if (definition == null) throw new InvalidOperationException("Research catalog contains a null practice.");
            if (string.IsNullOrWhiteSpace(definition.Id)) throw new InvalidOperationException("Practice requires an ID.");
            if (!Enum.IsDefined(typeof(PracticeEffectKind), definition.EffectKind)) {
                throw new InvalidOperationException($"Practice '{definition.Id}' has an unsupported effect kind.");
            }
            if (!rarities.ContainsKey(definition.RarityId)) throw new InvalidOperationException($"Practice '{definition.Id}' references unknown rarity '{definition.RarityId}'.");
            if (float.IsNaN(definition.SignatureThreshold) || float.IsInfinity(definition.SignatureThreshold) ||
                definition.SignatureThreshold <= 0f || definition.SignatureThreshold > 1f) {
                throw new InvalidOperationException($"Practice '{definition.Id}' has an invalid signature threshold.");
            }
            if (definition.EffectKind == PracticeEffectKind.InstantMoney) {
                if (definition.Modifiers != null && definition.Modifiers.Length > 0) throw new InvalidOperationException($"Instant practice '{definition.Id}' cannot contain modifiers.");
                if (definition.InstantMoney.Stored < 0d || definition.InstantMoney.Base.Degree < 0 ||
                    double.IsNaN(definition.InstantMoney.Stored) || double.IsInfinity(definition.InstantMoney.Stored)) {
                    throw new InvalidOperationException($"Instant practice '{definition.Id}' has an invalid money value.");
                }
                return;
            }
            if (float.IsNaN(definition.FailedSignatureDurationSeconds) || float.IsInfinity(definition.FailedSignatureDurationSeconds) ||
                definition.FailedSignatureDurationSeconds <= 0f) {
                throw new InvalidOperationException($"Modifier practice '{definition.Id}' requires a positive failed-signature duration.");
            }
            ModifierDefinition[] definitions = definition.Modifiers ?? Array.Empty<ModifierDefinition>();
            if (definitions.Length == 0) throw new InvalidOperationException($"Modifier practice '{definition.Id}' requires modifiers.");
            for (int index = 0; index < definitions.Length; index++) {
                ModifierDefinition modifierDefinition = definitions[index]
                    ?? throw new InvalidOperationException($"Practice '{definition.Id}' contains a null modifier definition.");
                if (modifierDefinition.NumericModifiers == null || modifierDefinition.NumericModifiers.Count == 0) {
                    throw new InvalidOperationException($"Practice '{definition.Id}' contains an empty modifier definition.");
                }
                for (int modifierIndex = 0; modifierIndex < modifierDefinition.NumericModifiers.Count; modifierIndex++) {
                    NumericModifierDefinition modifier = modifierDefinition.NumericModifiers[modifierIndex]
                        ?? throw new InvalidOperationException($"Practice '{definition.Id}' contains a null numeric modifier.");
                    modifier.ValidateConfiguration();
                }
            }
        }

        private bool HasClaimForPending() => _activeClaims.Count > 0;

        private bool IsValidClaim(PracticeDocumentClaim claim) {
            return claim.Epoch == _claimEpoch &&
                   _activeClaims.TryGetValue(claim.Token, out string practiceId) &&
                   string.Equals(practiceId, claim.PracticeId, StringComparison.Ordinal);
        }

        private void InvalidateClaims() {
            _claimEpoch++;
            _activeClaims.Clear();
        }

        private void NotifyChanged() => _changed.OnNext(Unit.Default);
        private void NotifyDocumentOffersChanged() => _documentOffersChanged.OnNext(Unit.Default);
        private static int RemainingSecondBucket(double seconds) => (int)Math.Min(int.MaxValue, Math.Ceiling(Math.Max(0d, seconds)));
        private static double FiniteOrZero(double value) => double.IsNaN(value) || double.IsInfinity(value) ? 0d : value;
        private static double SaturatingAdd(double left, double right) {
            double result = left + right;
            return double.IsNaN(result) || double.IsPositiveInfinity(result) ? double.MaxValue : Math.Max(0d, result);
        }
        private static float SaturatingFloat(double value) => value <= 0d || double.IsNaN(value) ? 0f : value >= float.MaxValue || double.IsPositiveInfinity(value) ? float.MaxValue : (float)value;
        private static Value MultiplyValueSafely(Value value, double multiplier) {
            if (value.IsZero || multiplier <= 0d || double.IsNaN(multiplier)) return Value.Zero;
            if (double.IsPositiveInfinity(multiplier)) return Value.Infinity;
            try { return value * multiplier; }
            catch (ArgumentException) { return Value.Infinity; }
        }
        private static double ResolveRequiredPoints(ResearchEntries entries, long cycles) {
            double basePoints = Math.Max(MinimumRequiredPoints, FiniteOrZero(entries.BaseRequiredPoints));
            double growth = Math.Max(0d, FiniteOrZero(entries.AdditionalRequiredPointsPerResolvedCycle));
            double additional = growth * Math.Max(0d, cycles);
            if (double.IsPositiveInfinity(additional)) return double.MaxValue;
            return Math.Max(MinimumRequiredPoints, SaturatingAdd(basePoints, additional));
        }

        internal readonly struct PracticeDocumentClaim {
            public int Epoch { get; }
            public long Token { get; }
            public string PracticeId { get; }
            public float SignatureThreshold { get; }

            public PracticeDocumentClaim(int epoch, long token, string practiceId, float signatureThreshold) {
                Epoch = epoch;
                Token = token;
                PracticeId = practiceId;
                SignatureThreshold = signatureThreshold;
            }
        }
    }
}
