using System.Collections.Generic;
using System.Linq;
using Services.Player;
using R3;
using Types.Economy;
using Types.Economy.Cost;
using Types.Upgrades;
using Types.Upgrades.Effects;
using UnityEngine;

namespace Services {
    public class UpgradeService : IService {

        private readonly Dictionary<string, ReactiveProperty<UpgradeNodeState>> _upgrades;
        private readonly Dictionary<string, List<string>> _parentIdsByChildId;
        private readonly Storage _storage;
        private readonly SessionContext _sessionContext;

        public Observable<Wallet> AffordabilityChanged => _storage.StructureMoney;

        public UpgradeService(Storage storage, SessionContext sessionContext) {
            _storage = storage;
            _sessionContext = sessionContext;
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

            foreach (var effect in state.Definition.Effects ?? Enumerable.Empty<UpgradeEffect>()) {
                effect?.Apply();
            }

            var nextLevel = state.Level + 1;
            var maxLevel = GetMaxLevel(state.Definition);
            var nextState = nextLevel >= maxLevel
                ? UpgradeNodeState.State.Completed
                : UpgradeNodeState.State.InProgress;

            PublishState(id, new UpgradeNodeState(state.Definition, nextLevel, nextState));

            RefreshChildAvailability(state.Definition);
            

            return true;
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
    }
}
