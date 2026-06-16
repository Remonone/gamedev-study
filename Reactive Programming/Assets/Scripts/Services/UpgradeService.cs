using System.Collections.Generic;
using System.Linq;
using Economy.Providers;
using Newtonsoft.Json.Linq;
using Services.Player;
using R3;
using Save;
using Types.Enums;
using Types.Enums.Cost;
using Types.Enums.Upgrades;
using Types.Enums.Upgrades.Effects;
using UnityEngine;

namespace Services {
    public class UpgradeService : IService, ISaveable {

        private readonly Dictionary<string, ReactiveProperty<UpgradeNodeState>> _upgrades;
        private readonly Dictionary<string, List<string>> _parentIdsByChildId;
        private readonly Storage _storage;
        private readonly ProviderRegistryService _providerRegistryService;
        private readonly InvalidationService _invalidationService;
        private readonly UnlockService _unlockService;

        public Observable<Wallet> AffordabilityChanged => _storage.StructureMoney;

        public UpgradeService(Storage storage, ProviderRegistryService providerRegistryService, InvalidationService invalidationService, UnlockService unlockService) {
            _storage = storage;
            _providerRegistryService = providerRegistryService;
            _invalidationService = invalidationService;
            _unlockService = unlockService;
            
            _upgrades = new Dictionary<string, ReactiveProperty<UpgradeNodeState>>();
            _parentIdsByChildId = new Dictionary<string, List<string>>();

            var upgrades = Resources.LoadAll<UpgradeNodeDefinition>("Upgrades");

            foreach (var upgrade in upgrades) {
                if (upgrade == null || string.IsNullOrWhiteSpace(upgrade.Id)) {
                    continue;
                }

                _upgrades.Add(upgrade.Id, new ReactiveProperty<UpgradeNodeState>(new UpgradeNodeState(upgrade)));
            }

            BuildDependencyIndex();
            InitializeAvailability();
        }

        public IReadOnlyList<UpgradeNodeState> GetAllUpgradeStates() {
            return _upgrades.Values
                .Select(upgrade => upgrade.Value)
                .OrderBy(upgrade => upgrade.Definition.Position.x)
                .ThenBy(upgrade => upgrade.Definition.Position.y)
                .ThenBy(upgrade => upgrade.Definition.Id)
                .ToArray();
        }

        public Observable<UpgradeNodeState> ObserveUpgradeState(string id) {
            if (!_upgrades.TryGetValue(id, out var upgrade)) {
                throw new KeyNotFoundException($"Upgrade with id '{id}' was not found.");
            }

            return upgrade;
        }

        public UpgradeNodeState GetUpgradeState(string id) {
            return _upgrades.TryGetValue(id, out var upgrade) ? upgrade.Value : null;
        }

        public Price GetUpgradePrice(string id) {
            var state = GetUpgradeState(id);
            return ResolvePrice(state);
        }

        public bool CanUpgrade(string id) {
            return CanUpgrade(GetUpgradeState(id));
        }

        public bool TryUpgrade(string id) {
            var state = GetUpgradeState(id);

            if (!CanUpgrade(state)) {
                return false;
            }

            _storage.Spend(ResolvePrice(state));
            
            var updatedState = new UpgradeNodeState(state);
            updatedState.Level++;
            var maxLevel = GetMaxLevel(updatedState.Definition);
            updatedState.CurrentState = updatedState.Level >= maxLevel
                ? UpgradeNodeState.State.Completed
                : UpgradeNodeState.State.InProgress;

            ApplyUpgrade(updatedState);
            
            PublishState(id, updatedState);

            RefreshChildAvailability(state.Definition);
            

            return true;
        }

        private void ApplyUpgrade(UpgradeNodeState updatedState) {
            if (UpgradeNodeDefinition.Category.Buff.Equals(updatedState.Definition.NodeCategory)) {
                ApplyModifiers(updatedState);
            }

            if (UpgradeNodeDefinition.Category.Unlock.Equals(updatedState.Definition.NodeCategory)) {
                UnlockItem(updatedState);
            }
        }

        private void UnlockItem(UpgradeNodeState updatedState) {
            foreach (var effect in updatedState.Definition.Effects
                         .Select(effect => effect as UnlockUpgrade)
                         .Where(effect => effect != null)) {
                _unlockService.UnlockItem(effect.UnlockId);
            }
        }

        private void ApplyModifiers(UpgradeNodeState updatedState) {
            var provider = _providerRegistryService.GetProvider<UpgradeModifierProvider>();
            provider.AddOrUpdate(updatedState);

            foreach (var effect in updatedState.Definition.Effects
                         .Select(effect => effect as ModifierUpgradeEffect)
                         .Where(effect => effect != null)) {

                var definitions = effect.Rules
                    .Where(rule => rule?.Target != null)
                    .ToList();
                
                foreach (var definition in definitions) {
                    _invalidationService.MarkDirtyByTarget(definition.Target);
                }
            }
        }

        private void BuildDependencyIndex() {
            foreach (var upgrade in _upgrades.Values.Select(upgrade => upgrade.Value)) {
                if (upgrade.Definition.ChildIds == null) {
                    continue;
                }

                foreach (var childId in upgrade.Definition.ChildIds) {
                    if (string.IsNullOrWhiteSpace(childId) || !_upgrades.ContainsKey(childId)) {
                        continue;
                    }

                    if (!_parentIdsByChildId.TryGetValue(childId, out var parentIds)) {
                        parentIds = new List<string>();
                        _parentIdsByChildId.Add(childId, parentIds);
                    }

                    parentIds.Add(upgrade.Definition.Id);
                }
            }
        }

        private void InitializeAvailability() {
            foreach (var pair in _upgrades) {
                var state = pair.Value.Value;
                var nextState = HasIncompleteDependencies(pair.Key)
                    ? UpgradeNodeState.State.Locked
                    : UpgradeNodeState.State.Available;

                PublishState(pair.Key, new UpgradeNodeState(state.Definition, state.Level, nextState));
            }
        }

        private void RefreshChildAvailability(UpgradeNodeDefinition definition) {
            if (definition.ChildIds == null) {
                return;
            }

            foreach (var childId in definition.ChildIds) {
                var childState = GetUpgradeState(childId);

                if (childState == null ||
                    childState.CurrentState != UpgradeNodeState.State.Locked ||
                    HasIncompleteDependencies(childId)) {
                    continue;
                }

                PublishState(childId, new UpgradeNodeState(
                    childState.Definition,
                    childState.Level,
                    UpgradeNodeState.State.Available));
            }
        }

        private bool HasIncompleteDependencies(string id) {
            if (!_parentIdsByChildId.TryGetValue(id, out var parentIds)) {
                return false;
            }

            return parentIds.Any(parentId =>
                GetUpgradeState(parentId)?.CurrentState is not (UpgradeNodeState.State.InProgress or UpgradeNodeState.State.Completed));
        }

        private void PublishState(string id, UpgradeNodeState state) {
            if (_upgrades.TryGetValue(id, out var upgrade)) {
                upgrade.Value = state;
            }
        }

        private bool CanUpgrade(UpgradeNodeState state) {
            if (state == null || state.Level >= GetMaxLevel(state.Definition)) {
                return false;
            }

            if (state.CurrentState == UpgradeNodeState.State.Locked ||
                state.CurrentState == UpgradeNodeState.State.Completed) {
                return false;
            }

            return _storage.CanAfford(ResolvePrice(state));
        }

        private static Price ResolvePrice(UpgradeNodeState state) {
            if (state?.Definition?.Price == null) {
                return new Price();
            }

            return state.Definition.Price.Evaluate(state.Level);
        }

        private static int GetMaxLevel(UpgradeNodeDefinition definition) {
            return Mathf.Max(1, definition.MaxLevel);
        }

        public string SaveKey => "Upgrades";
        public int Priority => 90;
        public JToken Save() {
            var upgrades = _upgrades.Values
                .Select(upgrade => upgrade.Value)
                .Where(upgrade => upgrade.Level > 0)
                .ToList();

            return new JObject(
                new JProperty("activeUpgrades", new JArray(
                    from upgrade in upgrades
                    select new JObject(
                        new JProperty("upgradeId", upgrade.Definition.Id),
                        new JProperty("level", upgrade.Level)
                    )
                ))
            );
        }

        public void Load(JToken data) {
            if (data?["activeUpgrades"] is JArray upgrades) {
                foreach (var upgradeData in upgrades.OfType<JObject>()) {
                    if (upgradeData["upgradeId"] == null ||
                        upgradeData["level"] == null) {
                        continue;
                    }
                    var upgrade = _upgrades[upgradeData["upgradeId"].Value<string>()].Value;
                    var updatedUpgrade = new UpgradeNodeState(upgrade);
                    updatedUpgrade.Level = upgradeData["level"].Value<int>();
                    ApplyUpgrade(upgrade);
                    PublishState(upgrade.Definition.Id, updatedUpgrade);
                }
                
            }
        }
    }
}
