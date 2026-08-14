using System;
using System.Collections.Generic;
using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Documents;
using Data.Enums;
using Data.Modifiers;
using Data.Results;
using Data.Upgrades;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using R3;
using Services.Locator;
using UnityEngine;
using Utils;

namespace Services {
    public class UpgradeService : IService, IInitialize, ISaveable {
        private Dictionary<string, UpgradeNodeState> _states = new(StringComparer.Ordinal);
        private HashSet<UpgradeNodeState> _ownedUpgrades = new();
        private List<PendingUpgradeRecord> _pendingUpgrades = new();
        private RestoreData _upgradeRestore;
        private ICacheInvalidator _cacheInvalidator;
        private bool _definitionsBuilt;
        private bool _isMutating;

        private readonly Dictionary<long, string> _activeClaims = new();
        private readonly HashSet<string> _claimedUpgradeIds = new(StringComparer.Ordinal);
        private int _claimEpoch;
        private long _nextClaimToken;
        private readonly Subject<Unit> _changed = new();
        private readonly Subject<Unit> _documentOffersChanged = new();

        private WalletService _wallet;
        private IAssetListLease<UpgradeNodeDefinition> _lease;
        private IAssetProvider _assetProvider;

        public IReadOnlyCollection<UpgradeNodeState> OwnedUpgrades => _ownedUpgrades;
        public IReadOnlyCollection<UpgradeNodeState> Nodes => _states.Values;
        public Observable<Unit> Changed => _changed;
        public Observable<Unit> DocumentOffersChanged => _documentOffersChanged;

        public UpgradeService() { }

        internal UpgradeService(IAssetProvider assetProvider) {
            _assetProvider = assetProvider ?? throw new ArgumentNullException(nameof(assetProvider));
        }

        public async UniTask InitializeAsync(IServiceScope scope) {
            _cacheInvalidator = scope.Get<ICacheInvalidator>();
            _wallet = scope.Get<WalletService>();
            _assetProvider ??= scope.Container.Get<IAssetProvider>();
            _lease = await _assetProvider.LoadAssetsByLabelAsync<UpgradeNodeDefinition>(AddressableConstants.UPGRADE_LABEL);
            BuildDefinitions(_lease.Assets);
            RestoreOnPresent();
        }

        private void RestoreOnPresent() {
            if (_upgradeRestore == null) return;
            ApplyRestore(_upgradeRestore, true);
            _upgradeRestore = null;
            NotifyDocumentOffersChanged();
        }

        internal void BuildDefinitions(IReadOnlyList<UpgradeNodeDefinition> assets) {
            if (assets == null) throw new ArgumentNullException(nameof(assets));

            var states = new Dictionary<string, UpgradeNodeState>(StringComparer.Ordinal);
            foreach (UpgradeNodeDefinition asset in assets) {
                if (asset == null) ValidateDefinition(asset);
                if (asset.GetType() != typeof(UpgradeNodeDefinition)) {
                    Debug.LogWarning(
                        $"Upgrade subtype '{asset.GetType().Name}' with ID '{asset.Id}' was assigned the ordinary " +
                        "upgrade label and was excluded.");
                    continue;
                }
                ValidateDefinition(asset);
                if (!states.TryAdd(asset.Id, new UpgradeNodeState(asset))) {
                    throw new InvalidOperationException($"Duplicate upgrade ID '{asset.Id}'.");
                }
            }

            _states = states;
            _ownedUpgrades = new HashSet<UpgradeNodeState>();
            _pendingUpgrades = new List<PendingUpgradeRecord>();
            InvalidateAllClaims();
            _definitionsBuilt = true;
        }

        public bool CanUpgrade(string upgradeId) {
            UpgradeNodeState upgrade = GetUpgrade(upgradeId);
            if (upgrade == null || _wallet == null ||
                upgrade.CurrentState is not (UpgradeNodeState.State.Available or UpgradeNodeState.State.InProgress) ||
                upgrade.Definition.IsTerminalLevel(upgrade.Level)) {
                return false;
            }

            return _wallet.CanAfford(ResolvePrice(upgrade));
        }

        public bool TryUpgrade(string upgradeId) {
            if (_isMutating || !CanUpgrade(upgradeId)) return false;

            UpgradeNodeState upgrade = GetUpgrade(upgradeId);
            Value price = ResolvePrice(upgrade);
            var updated = new UpgradeNodeState(upgrade);
            bool firstPurchase = updated.Level == 0;

            if (firstPurchase) {
                updated.CurrentState = UpgradeNodeState.State.Pending;
                updated.Effectiveness = 0f;
            }
            else {
                updated.Level++;
                updated.CurrentState = updated.Definition.IsTerminalLevel(updated.Level)
                    ? UpgradeNodeState.State.Completed
                    : UpgradeNodeState.State.InProgress;
            }

            _isMutating = true;
            bool documentOffersChanged = false;
            try {
                if (!_wallet.TryWithdrawWallet(price, false)) return false;

                _states[upgradeId] = updated;
                if (firstPurchase) {
                    _pendingUpgrades.Add(new PendingUpgradeRecord(upgradeId, price));
                    documentOffersChanged = true;
                }
                else {
                    _ownedUpgrades.Remove(upgrade);
                    _ownedUpgrades.Add(updated);
                    InvalidateGroups(updated.Definition.Modifiers);
                }

                _wallet.NotifyBalanceChanged();
                NotifyChanged();
                return true;
            }
            finally {
                _isMutating = false;
                if (documentOffersChanged) NotifyDocumentOffersChanged();
            }
        }

        internal bool TryClaimPendingUpgrade(out UpgradeDocumentClaim claim) {
            claim = default;
            return TryPeekPendingUpgradeDocument(out DocumentOffer offer) &&
                   TryClaimPendingUpgrade(offer.Key.DomainId, out claim);
        }

        internal bool TryPeekPendingUpgradeDocument(out DocumentOffer offer) {
            offer = null;
            if (_isMutating) return false;

            for (int index = 0; index < _pendingUpgrades.Count; index++) {
                string upgradeId = _pendingUpgrades[index].UpgradeId;
                if (_claimedUpgradeIds.Contains(upgradeId)) continue;

                UpgradeNodeState upgrade = GetUpgrade(upgradeId);
                if (upgrade == null) continue;

                offer = new DocumentOffer(
                    new DocumentOfferKey(DocumentKind.Upgrade, upgradeId),
                    true,
                    upgrade.Definition.Name,
                    upgrade.Definition.Icon);
                return true;
            }

            return false;
        }

        internal bool TryClaimPendingUpgrade(string requestedUpgradeId, out UpgradeDocumentClaim claim) {
            claim = default;
            if (_isMutating || string.IsNullOrWhiteSpace(requestedUpgradeId)) return false;

            for (int index = 0; index < _pendingUpgrades.Count; index++) {
                string upgradeId = _pendingUpgrades[index].UpgradeId;
                if (!string.Equals(upgradeId, requestedUpgradeId, StringComparison.Ordinal) ||
                    _claimedUpgradeIds.Contains(upgradeId)) {
                    continue;
                }

                long token = ++_nextClaimToken;
                _activeClaims.Add(token, upgradeId);
                _claimedUpgradeIds.Add(upgradeId);
                claim = new UpgradeDocumentClaim(_claimEpoch, token, upgradeId);
                NotifyDocumentOffersChanged();
                return true;
            }

            return false;
        }

        internal bool TryCompletePendingUpgrade(
            UpgradeDocumentClaim claim,
            SignatureEvaluationResult result) {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (_isMutating || !IsValidClaim(claim)) return false;

            int pendingIndex = FindPendingIndex(claim.UpgradeId);
            UpgradeNodeState upgrade = GetUpgrade(claim.UpgradeId);
            if (pendingIndex < 0 || upgrade == null ||
                upgrade.Level != 0 || upgrade.CurrentState != UpgradeNodeState.State.Pending) {
                ReleaseClaim(claim);
                NotifyDocumentOffersChanged();
                return false;
            }

            var updated = new UpgradeNodeState(upgrade) {
                Level = 1,
                Effectiveness = ResolveEffectiveness(result)
            };
            updated.CurrentState = updated.Definition.IsTerminalLevel(updated.Level)
                ? UpgradeNodeState.State.Completed
                : UpgradeNodeState.State.InProgress;

            _isMutating = true;
            bool documentOffersChanged = false;
            try {
                ReleaseClaim(claim);
                _pendingUpgrades.RemoveAt(pendingIndex);
                _states[claim.UpgradeId] = updated;
                _ownedUpgrades.Add(updated);
                InvalidateGroups(updated.Definition.Modifiers);
                NotifyChanged();
                documentOffersChanged = true;
                return true;
            }
            finally {
                _isMutating = false;
                if (documentOffersChanged) NotifyDocumentOffersChanged();
            }
        }

        internal bool TryReleasePendingUpgrade(UpgradeDocumentClaim claim) {
            if (!IsValidClaim(claim)) return false;
            ReleaseClaim(claim);
            NotifyDocumentOffersChanged();
            return true;
        }

        private bool IsValidClaim(UpgradeDocumentClaim claim) {
            return claim.Epoch == _claimEpoch &&
                   _activeClaims.TryGetValue(claim.Token, out string upgradeId) &&
                   string.Equals(upgradeId, claim.UpgradeId, StringComparison.Ordinal);
        }

        private void ReleaseClaim(UpgradeDocumentClaim claim) {
            _activeClaims.Remove(claim.Token);
            _claimedUpgradeIds.Remove(claim.UpgradeId);
        }

        private int FindPendingIndex(string upgradeId) {
            for (int index = 0; index < _pendingUpgrades.Count; index++) {
                if (string.Equals(_pendingUpgrades[index].UpgradeId, upgradeId, StringComparison.Ordinal)) {
                    return index;
                }
            }

            return -1;
        }

        private static float ResolveEffectiveness(SignatureEvaluationResult result) {
            float similarity = result.Similarity;
            float minimum = result.MinimumSimilarity;
            if (!IsFinite(similarity) || !IsFinite(minimum) || minimum <= 0f) {
                return result.Status == SignatureEvaluationStatus.Accepted ? 1f : 0f;
            }

            return Mathf.Clamp01(similarity / minimum);
        }

        private void NotifyChanged() {
            _changed.OnNext(Unit.Default);
        }

        private void NotifyDocumentOffersChanged() {
            _documentOffersChanged.OnNext(Unit.Default);
        }

        private void InvalidateGroups(ModifierDefinition[] definitionModifiers) {
            if (definitionModifiers == null || _cacheInvalidator == null) return;
            var affectedGroups = new HashSet<Type>();
            foreach (ModifierDefinition modifier in definitionModifiers) {
                if (modifier != null) affectedGroups.UnionWith(modifier.GetAffectedTypes());
            }

            foreach (Type group in affectedGroups) _cacheInvalidator.Invalidate(group);
        }

        public Value ResolvePrice(UpgradeNodeState state) {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return state.Definition.CostFormula.Evaluate(state.Level);
        }

        public UpgradeNodeState GetUpgrade(string upgradeId) {
            if (string.IsNullOrWhiteSpace(upgradeId)) return null;
            return _states.GetValueOrDefault(upgradeId);
        }

        internal void ApplyAvailabilityBatch(IReadOnlyDictionary<string, bool> availability) {
            if (availability == null) throw new ArgumentNullException(nameof(availability));

            foreach (KeyValuePair<string, UpgradeNodeState> pair in _states) {
                UpgradeNodeState state = pair.Value;
                if (state.Level != 0 ||
                    state.CurrentState is not (UpgradeNodeState.State.Locked or UpgradeNodeState.State.Available)) {
                    continue;
                }

                bool isAvailable = availability.TryGetValue(pair.Key, out bool value) && value;
                state.CurrentState = isAvailable
                    ? UpgradeNodeState.State.Available
                    : UpgradeNodeState.State.Locked;
            }
        }

        public string SaveId => "Upgrades";

        public JToken Serialize() {
            var owned = new JArray();
            foreach (UpgradeNodeState upgrade in _ownedUpgrades) {
                owned.Add(new JObject {
                    ["id"] = upgrade.Definition.Id,
                    ["level"] = upgrade.Level,
                    ["effectiveness"] = upgrade.Effectiveness
                });
            }

            var pending = new JArray();
            for (int index = 0; index < _pendingUpgrades.Count; index++) {
                PendingUpgradeRecord upgrade = _pendingUpgrades[index];
                pending.Add(new JObject {
                    ["id"] = upgrade.UpgradeId,
                    ["paidStored"] = upgrade.PaidPrice.Stored,
                    ["paidDegree"] = upgrade.PaidPrice.Base.Degree
                });
            }

            return new JObject {
                ["upgrades"] = owned,
                ["pendingUpgrades"] = pending
            };
        }

        public void Deserialize(JToken state) {
            if (_isMutating) throw new InvalidOperationException("Cannot restore upgrades during another upgrade mutation.");
            RestoreData restored = ParseRestore(state);
            if (!_definitionsBuilt) {
                _upgradeRestore = restored;
                return;
            }

            ApplyRestore(restored, true);
            NotifyDocumentOffersChanged();
        }

        public void Dispose() {
            _changed.Dispose();
            _documentOffersChanged.Dispose();
            InvalidateAllClaims();
            _states.Clear();
            _ownedUpgrades.Clear();
            _pendingUpgrades.Clear();
            _upgradeRestore = null;
            _lease?.Dispose();
            _lease = null;
        }

        private void ApplyRestore(RestoreData restored, bool notify) {
            if (_isMutating) throw new InvalidOperationException("Cannot restore upgrades during another upgrade mutation.");

            var nextStates = new Dictionary<string, UpgradeNodeState>(_states.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, UpgradeNodeState> pair in _states) {
                nextStates.Add(pair.Key, new UpgradeNodeState(pair.Value.Definition));
            }

            var nextOwned = new HashSet<UpgradeNodeState>();
            foreach (KeyValuePair<string, OwnedUpgradeRestore> pair in restored.Owned) {
                if (!nextStates.TryGetValue(pair.Key, out UpgradeNodeState state)) {
                    Debug.LogWarning($"Saved upgrade '{pair.Key}' is not present in the loaded catalog and was ignored.");
                    continue;
                }

                int level = pair.Value.Level;
                if (state.Definition.HasLevelCap && level > state.Definition.MaxLevel) {
                    Debug.LogWarning(
                        $"Saved level {level} for upgrade '{pair.Key}' exceeds its maximum and was clamped.");
                    level = state.Definition.MaxLevel;
                }

                state.Level = level;
                state.Effectiveness = pair.Value.Effectiveness;
                state.CurrentState = state.Definition.IsTerminalLevel(level)
                    ? UpgradeNodeState.State.Completed
                    : UpgradeNodeState.State.InProgress;
                nextOwned.Add(state);
            }

            var nextPending = new List<PendingUpgradeRecord>();
            var refunds = new List<PendingUpgradeRecord>();
            for (int index = 0; index < restored.Pending.Count; index++) {
                PendingUpgradeRecord pending = restored.Pending[index];
                if (!nextStates.TryGetValue(pending.UpgradeId, out UpgradeNodeState state)) {
                    refunds.Add(pending);
                    continue;
                }

                state.Level = 0;
                state.Effectiveness = 0f;
                state.CurrentState = UpgradeNodeState.State.Pending;
                nextPending.Add(pending);
            }

            _isMutating = true;
            try {
                InvalidateAllClaims();
                _states = nextStates;
                _ownedUpgrades = nextOwned;
                _pendingUpgrades = nextPending;
                _cacheInvalidator?.InvalidateAll();

                for (int index = 0; index < refunds.Count; index++) {
                    PendingUpgradeRecord refund = refunds[index];
                    Debug.LogWarning(
                        $"Pending upgrade '{refund.UpgradeId}' is not present in the loaded catalog; its paid cost was refunded.");
                    _wallet.ReplenishWallet(refund.PaidPrice);
                }

                if (notify) NotifyChanged();
            }
            finally {
                _isMutating = false;
            }
        }

        private static RestoreData ParseRestore(JToken state) {
            if (state is not JObject root || root["upgrades"] is not JArray upgrades) {
                throw new JsonSerializationException("Upgrade state must contain an upgrades array.");
            }

            JToken pendingToken = root["pendingUpgrades"];
            if (pendingToken != null && pendingToken is not JArray) {
                throw new JsonSerializationException("Pending upgrade state must be an array.");
            }

            var restored = new RestoreData();
            var allIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (JToken upgrade in upgrades) {
                if (upgrade is not JObject data || data["id"]?.Type != JTokenType.String ||
                    data["level"]?.Type != JTokenType.Integer) {
                    throw new JsonSerializationException(
                        "Each saved upgrade must contain a string ID and integer level.");
                }

                string id = data["id"].Value<string>();
                int level = data["level"].Value<int>();
                float effectiveness = 1f;
                if (data["effectiveness"] != null &&
                    !TryReadNumber(data["effectiveness"], out effectiveness)) {
                    throw new JsonSerializationException("Saved upgrade effectiveness must be numeric.");
                }

                if (string.IsNullOrWhiteSpace(id) || level <= 0 || !IsFinite(effectiveness) ||
                    effectiveness < 0f || effectiveness > 1f) {
                    throw new JsonSerializationException("Saved upgrade data contains values outside valid ranges.");
                }

                if (!allIds.Add(id)) throw new JsonSerializationException($"Duplicate saved upgrade ID '{id}'.");
                restored.Owned.Add(id, new OwnedUpgradeRestore(level, effectiveness));
            }

            if (pendingToken is JArray pendingUpgrades) {
                foreach (JToken upgrade in pendingUpgrades) {
                    if (upgrade is not JObject data || data["id"]?.Type != JTokenType.String ||
                        !TryReadValue(data["paidStored"], data["paidDegree"], out Value paidPrice)) {
                        throw new JsonSerializationException(
                            "Each pending upgrade must contain an ID and a canonical paid cost.");
                    }

                    string id = data["id"].Value<string>();
                    if (string.IsNullOrWhiteSpace(id) || paidPrice.IsZero) {
                        throw new JsonSerializationException("Pending upgrade data contains values outside valid ranges.");
                    }

                    if (!allIds.Add(id)) throw new JsonSerializationException($"Duplicate saved upgrade ID '{id}'.");
                    restored.Pending.Add(new PendingUpgradeRecord(id, paidPrice));
                }
            }

            return restored;
        }

        private static bool TryReadValue(JToken storedToken, JToken degreeToken, out Value value) {
            value = default;
            if (!TryReadNumber(storedToken, out double stored) || degreeToken?.Type != JTokenType.Integer) {
                return false;
            }

            int degree = degreeToken.Value<int>();
            bool invalidStored = double.IsNaN(stored) || double.IsInfinity(stored) || stored <= 0d || stored >= 1000d;
            bool invalidDegree = degree < 0 || degree > 0 && stored < 1d;
            if (invalidStored || invalidDegree) return false;

            var candidate = new Value(stored, new BaseValue(degree));
            if (candidate.Stored != stored || candidate.Base.Degree != degree) return false;
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

        private static bool IsFinite(float value) {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool TryReadNumber(JToken token, out double value) {
            if (token?.Type is JTokenType.Integer or JTokenType.Float) {
                value = token.Value<double>();
                return true;
            }

            value = default;
            return false;
        }

        private void InvalidateAllClaims() {
            _claimEpoch++;
            _activeClaims.Clear();
            _claimedUpgradeIds.Clear();
        }

        private static void ValidateDefinition(UpgradeNodeDefinition definition) {
            if (definition == null) throw new InvalidOperationException("Upgrade catalog contains a null definition.");
            if (string.IsNullOrWhiteSpace(definition.Id)) {
                throw new InvalidOperationException($"Upgrade definition '{definition.name}' has an empty ID.");
            }

            if (definition.MaxLevel < 0) {
                throw new InvalidOperationException($"Upgrade '{definition.Id}' cannot have a negative maximum level.");
            }

            if (definition.CostFormula == null) {
                throw new InvalidOperationException($"Upgrade '{definition.Id}' has no cost formula.");
            }

            ValidateModifierIds(definition);
        }

        private static void ValidateModifierIds(UpgradeNodeDefinition definition) {
            if (definition.Modifiers == null) return;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int definitionIndex = 0; definitionIndex < definition.Modifiers.Length; definitionIndex++) {
                ModifierDefinition modifierDefinition = definition.Modifiers[definitionIndex];
                if (modifierDefinition?.NumericModifiers == null) continue;
                for (int modifierIndex = 0; modifierIndex < modifierDefinition.NumericModifiers.Count; modifierIndex++) {
                    NumericModifierDefinition modifier = modifierDefinition.NumericModifiers[modifierIndex];
                    if (modifier == null) continue;
                    if (string.IsNullOrWhiteSpace(modifier.Id)) {
                        throw new InvalidOperationException(
                            $"Upgrade '{definition.Id}' has a numeric modifier without an ID.");
                    }

                    if (!ids.Add(modifier.Id)) {
                        throw new InvalidOperationException(
                            $"Upgrade '{definition.Id}' has duplicate numeric modifier ID '{modifier.Id}'.");
                    }
                }
            }
        }

        private sealed class RestoreData {
            public readonly Dictionary<string, OwnedUpgradeRestore> Owned = new(StringComparer.Ordinal);
            public readonly List<PendingUpgradeRecord> Pending = new();
        }

        private readonly struct OwnedUpgradeRestore {
            public int Level { get; }
            public float Effectiveness { get; }

            public OwnedUpgradeRestore(int level, float effectiveness) {
                Level = level;
                Effectiveness = effectiveness;
            }
        }

        private readonly struct PendingUpgradeRecord {
            public string UpgradeId { get; }
            public Value PaidPrice { get; }

            public PendingUpgradeRecord(string upgradeId, Value paidPrice) {
                UpgradeId = upgradeId;
                PaidPrice = paidPrice;
            }
        }

        internal readonly struct UpgradeDocumentClaim {
            internal int Epoch { get; }
            internal long Token { get; }
            internal string UpgradeId { get; }

            internal UpgradeDocumentClaim(int epoch, long token, string upgradeId) {
                Epoch = epoch;
                Token = token;
                UpgradeId = upgradeId;
            }
        }
    }
}
