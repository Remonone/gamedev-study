using System;
using System.Collections.Generic;
using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Modifiers;
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
        private Dictionary<string, int> _upgradeRestore;
        private ICacheInvalidator _cacheInvalidator;
        private bool _definitionsBuilt;
        
        private HashSet<UpgradeNodeState> _ownedUpgrades = new();
        private readonly Subject<Unit> _changed = new();
        
        private WalletService _wallet;
        private IAssetListLease<UpgradeNodeDefinition> _lease;
        private IAssetProvider _assetProvider;
        
        public IReadOnlyCollection<UpgradeNodeState> OwnedUpgrades => _ownedUpgrades;
        public IReadOnlyCollection<UpgradeNodeState> Nodes => _states.Values;
        public Observable<Unit> Changed => _changed;

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
            ApplyRestore(_upgradeRestore, false);
            _upgradeRestore = null;
        }

        internal void BuildDefinitions(IReadOnlyList<UpgradeNodeDefinition> assets) {
            if (assets == null) throw new ArgumentNullException(nameof(assets));

            var states = new Dictionary<string, UpgradeNodeState>(StringComparer.Ordinal);
            foreach (var asset in assets) {
                ValidateDefinition(asset);
                if (!states.TryAdd(asset.Id, new UpgradeNodeState(asset))) {
                    throw new InvalidOperationException($"Duplicate upgrade ID '{asset.Id}'.");
                }
            }

            _states = states;
            _ownedUpgrades = new HashSet<UpgradeNodeState>();
            _definitionsBuilt = true;
        }


        public bool TryUpgrade(string upgradeId) {
            var upgrade = GetUpgrade(upgradeId);
            if (upgrade == null || upgrade.CurrentState is not (UpgradeNodeState.State.Available or UpgradeNodeState.State.InProgress)) {
                return false;
            }

            var price = ResolvePrice(upgrade);
            if (!_wallet.TryWithdrawWallet(price)) {
                return false;
            }
            var updated = new UpgradeNodeState(upgrade);

            updated.Level++;
            updated.CurrentState = updated.Level >= updated.Definition.MaxLevel
                ? UpgradeNodeState.State.Completed
                : UpgradeNodeState.State.InProgress;
            
            _ownedUpgrades.Remove(upgrade);
            _ownedUpgrades.Add(updated);
            _states[upgradeId] = updated;
            
            InvalidateGroups(updated.Definition.Modifiers);

            NotifyChanged();
            return true;
        }

        private void NotifyChanged() {
            _changed.OnNext(Unit.Default);
        }


        private void InvalidateGroups(ModifierDefinition[] definitionModifiers) {
            if (definitionModifiers == null || _cacheInvalidator == null) return;
            var affectedGroups = new HashSet<Type>();
            foreach (var modifier in definitionModifiers) {
                affectedGroups.UnionWith(modifier.GetAffectedTypes());
            }

            foreach (var group in affectedGroups) {
                _cacheInvalidator.Invalidate(group);
            }
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
            var result = new JObject();
            var list = new JArray();
            result.Add("upgrades", list);
            foreach (var upgrade in _ownedUpgrades) {
                list.Add(new JObject {
                    ["id"] = upgrade.Definition.Id,
                    ["level"] = upgrade.Level
                });
            }

            return result;
        }

        public void Deserialize(JToken state) {
            if (state is not JObject root || root["upgrades"] is not JArray upgrades) {
                throw new JsonSerializationException("Upgrade state must contain an upgrades array.");
            }

            var restored = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (JToken upgrade in upgrades) {
                if (upgrade is not JObject data || data["id"]?.Type != JTokenType.String ||
                    data["level"]?.Type != JTokenType.Integer) {
                    throw new JsonSerializationException("Each saved upgrade must contain a string ID and integer level.");
                }

                string id = data["id"].Value<string>();
                int level = data["level"].Value<int>();
                if (string.IsNullOrWhiteSpace(id) || level <= 0) {
                    throw new JsonSerializationException("Saved upgrade IDs must be non-empty and levels must be positive.");
                }

                if (!restored.TryAdd(id, level)) {
                    throw new JsonSerializationException($"Duplicate saved upgrade ID '{id}'.");
                }
            }

            if (!_definitionsBuilt) {
                _upgradeRestore = restored;
                return;
            }

            ApplyRestore(restored, true);
        }
        
        public void Dispose() {
            _changed.Dispose();
            _states.Clear();
            _ownedUpgrades.Clear();
            _upgradeRestore?.Clear();
            _upgradeRestore = null;
            _lease?.Dispose();
            _lease = null;
        }

        private void ApplyRestore(IReadOnlyDictionary<string, int> restored, bool notify) {
            var nextStates = new Dictionary<string, UpgradeNodeState>(_states.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, UpgradeNodeState> pair in _states) {
                nextStates.Add(pair.Key, new UpgradeNodeState(pair.Value.Definition));
            }

            var nextOwned = new HashSet<UpgradeNodeState>();
            foreach (KeyValuePair<string, int> pair in restored) {
                if (!nextStates.TryGetValue(pair.Key, out UpgradeNodeState state)) {
                    Debug.LogWarning($"Saved upgrade '{pair.Key}' is not present in the loaded catalog and was ignored.");
                    continue;
                }

                int level = pair.Value;
                if (level > state.Definition.MaxLevel) {
                    Debug.LogWarning(
                        $"Saved level {level} for upgrade '{pair.Key}' exceeds its maximum and was clamped.");
                    level = state.Definition.MaxLevel;
                }

                state.Level = level;
                state.CurrentState = level >= state.Definition.MaxLevel
                    ? UpgradeNodeState.State.Completed
                    : UpgradeNodeState.State.InProgress;
                nextOwned.Add(state);
            }

            var affectedGroups = new HashSet<Type>();
            CollectAffectedGroups(_ownedUpgrades, affectedGroups);
            CollectAffectedGroups(nextOwned, affectedGroups);
            _states = nextStates;
            _ownedUpgrades = nextOwned;
            InvalidateGroups(affectedGroups);
            if (notify) NotifyChanged();
        }

        private static void CollectAffectedGroups(IEnumerable<UpgradeNodeState> upgrades, HashSet<Type> affectedGroups) {
            foreach (UpgradeNodeState upgrade in upgrades) {
                ModifierDefinition[] modifiers = upgrade.Definition.Modifiers;
                if (modifiers == null) continue;
                foreach (ModifierDefinition modifier in modifiers) {
                    if (modifier != null) affectedGroups.UnionWith(modifier.GetAffectedTypes());
                }
            }
        }

        private void InvalidateGroups(HashSet<Type> affectedGroups) {
            if (_cacheInvalidator == null) return;
            foreach (Type group in affectedGroups) _cacheInvalidator.Invalidate(group);
        }

        private static void ValidateDefinition(UpgradeNodeDefinition definition) {
            if (definition == null) throw new InvalidOperationException("Upgrade catalog contains a null definition.");
            if (string.IsNullOrWhiteSpace(definition.Id)) {
                throw new InvalidOperationException($"Upgrade definition '{definition.name}' has an empty ID.");
            }

            if (definition.MaxLevel <= 0) {
                throw new InvalidOperationException($"Upgrade '{definition.Id}' must have a positive maximum level.");
            }

            if (definition.CostFormula == null) {
                throw new InvalidOperationException($"Upgrade '{definition.Id}' has no cost formula.");
            }
        }
    }
}
