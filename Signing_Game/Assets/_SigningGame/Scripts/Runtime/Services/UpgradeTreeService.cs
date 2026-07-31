using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Data.Upgrades;
using R3;
using Services.Locator;
using UnityEngine;
using Utils;

namespace Services {
    public sealed class UpgradeTreeConnection {
        public string ParentId { get; }
        public string ChildId { get; }
        public UpgradeNodeState Parent { get; internal set; }
        public UpgradeNodeState Child { get; internal set; }
        public bool DrawEdge { get; }

        internal UpgradeTreeConnection(UpgradeNodeState parent, UpgradeNodeState child, bool drawEdge) {
            ParentId = parent.Definition.Id;
            ChildId = child.Definition.Id;
            Parent = parent;
            Child = child;
            DrawEdge = drawEdge;
        }
    }

    public sealed class UpgradeTreeService : IService, IInitialize {
        private readonly Subject<Unit> _changed = new();
        private readonly CompositeDisposable _subscriptions = new();
        private readonly List<UpgradeTreeConnection> _connections = new();
        private readonly Dictionary<string, List<string>> _parents = new(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _unlocked = new(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _visible = new(StringComparer.Ordinal);
        private readonly HashSet<string> _invalidUnlockNodes = new(StringComparer.Ordinal);

        private UpgradeService _upgradeService;
        private GameStatisticsService _statistics;

        public IReadOnlyCollection<UpgradeNodeState> Nodes => _upgradeService?.Nodes ?? Array.Empty<UpgradeNodeState>();
        public IReadOnlyList<UpgradeTreeConnection> Connections => _connections;
        public Observable<Unit> Changed => _changed;

        public UniTask InitializeAsync(IServiceScope scope) {
            _upgradeService = scope.Get<UpgradeService>();
            _statistics = scope.Get<GameStatisticsService>();

            BuildGraphIndex();
            _upgradeService.Changed.Subscribe(_ => Reevaluate()).AddTo(_subscriptions);
            _statistics.Changed.Subscribe(_ => Reevaluate()).AddTo(_subscriptions);
            Reevaluate();
            return UniTask.CompletedTask;
        }

        public bool IsUnlocked(string upgradeId) {
            return !string.IsNullOrWhiteSpace(upgradeId) &&
                   _unlocked.TryGetValue(upgradeId, out bool value) && value;
        }

        public bool IsVisible(string upgradeId) {
            return !string.IsNullOrWhiteSpace(upgradeId) &&
                   _visible.TryGetValue(upgradeId, out bool value) && value;
        }

        public Value ResolvePrice(UpgradeNodeState state) {
            return _upgradeService.ResolvePrice(state);
        }

        public void Dispose() {
            _subscriptions.Dispose();
            _changed.Dispose();
            _connections.Clear();
            _parents.Clear();
            _unlocked.Clear();
            _visible.Clear();
            _invalidUnlockNodes.Clear();
            _upgradeService = null;
            _statistics = null;
        }

        internal void Reevaluate() {
            if (_upgradeService == null || _statistics == null) return;

            var availability = new Dictionary<string, bool>(_upgradeService.Nodes.Count, StringComparer.Ordinal);
            _unlocked.Clear();
            _visible.Clear();

            for (int index = 0; index < _connections.Count; index++) {
                UpgradeTreeConnection connection = _connections[index];
                connection.Parent = _upgradeService.GetUpgrade(connection.ParentId);
                connection.Child = _upgradeService.GetUpgrade(connection.ChildId);
            }

            foreach (UpgradeNodeState state in _upgradeService.Nodes) {
                bool unlocked = state.Level > 0 || EvaluateRequirements(state);
                bool visible = unlocked || ResolveLockedVisibility(state.Definition);
                availability.Add(state.Definition.Id, unlocked);
                _unlocked.Add(state.Definition.Id, unlocked);
                _visible.Add(state.Definition.Id, visible);
            }

            _upgradeService.ApplyAvailabilityBatch(availability);
            _changed.OnNext(Unit.Default);
        }

        private void BuildGraphIndex() {
            _connections.Clear();
            _parents.Clear();
            _invalidUnlockNodes.Clear();

            var catalog = new Dictionary<string, UpgradeNodeState>(StringComparer.Ordinal);
            foreach (UpgradeNodeState state in _upgradeService.Nodes) {
                catalog.Add(state.Definition.Id, state);
                _parents.Add(state.Definition.Id, new List<string>());
                ValidateNodeConfiguration(state.Definition);
            }

            foreach (UpgradeNodeState parent in _upgradeService.Nodes) {
                UpgradeNodeLink[] links = parent.Definition.Children;
                if (links == null || links.Length == 0) continue;

                var linkedChildren = new HashSet<string>(StringComparer.Ordinal);
                for (int index = 0; index < links.Length; index++) {
                    UpgradeNodeDefinition linkedDefinition = links[index].Child;
                    if (linkedDefinition == null) {
                        WarnInvalidLink(parent.Definition.Id, index, "has no child definition");
                        continue;
                    }

                    string childId = linkedDefinition.Id;
                    if (string.IsNullOrWhiteSpace(childId)) {
                        WarnInvalidLink(parent.Definition.Id, index, "references a child with an empty ID");
                        continue;
                    }

                    if (string.Equals(parent.Definition.Id, childId, StringComparison.Ordinal)) {
                        WarnInvalidLink(parent.Definition.Id, index, "is a self-link");
                        continue;
                    }

                    if (!catalog.TryGetValue(childId, out UpgradeNodeState child)) {
                        WarnInvalidLink(parent.Definition.Id, index,
                            $"references unloaded child '{childId}'");
                        continue;
                    }

                    if (!linkedChildren.Add(childId)) {
                        WarnInvalidLink(parent.Definition.Id, index,
                            $"duplicates child '{childId}'");
                        continue;
                    }

                    _parents[childId].Add(parent.Definition.Id);
                    _connections.Add(new UpgradeTreeConnection(parent, child, links[index].DrawEdge));
                }
            }
        }

        private void ValidateNodeConfiguration(UpgradeNodeDefinition definition) {
            bool invalid = false;
            if (!Enum.IsDefined(typeof(ParentUnlockMode), definition.ParentUnlockMode)) {
                Debug.LogWarning($"Upgrade '{definition.Id}' has an invalid parent unlock mode and will stay locked.");
                invalid = true;
            }

            if (!Enum.IsDefined(typeof(StatisticRequirementMode), definition.StatisticRequirementMode)) {
                Debug.LogWarning($"Upgrade '{definition.Id}' has an invalid statistic requirement mode and will stay locked.");
                invalid = true;
            }

            if (!Enum.IsDefined(typeof(LockedNodeDisplayMode), definition.LockedDisplayMode)) {
                Debug.LogWarning(
                    $"Upgrade '{definition.Id}' has an invalid locked display mode; VisibleLocked will be used.");
            }

            GameStatisticRequirement[] requirements = definition.StatisticRequirements;
            if (requirements != null) {
                for (int index = 0; index < requirements.Length; index++) {
                    GameStatisticRequirement requirement = requirements[index];
                    if (string.IsNullOrWhiteSpace(requirement.StatisticId) ||
                        double.IsNaN(requirement.TargetValue) || double.IsInfinity(requirement.TargetValue) ||
                        !Enum.IsDefined(typeof(StatisticComparison), requirement.Comparison)) {
                        Debug.LogWarning(
                            $"Upgrade '{definition.Id}' has an invalid statistic requirement at index {index} and will stay locked.");
                        invalid = true;
                    }
                }
            }

            if (invalid) _invalidUnlockNodes.Add(definition.Id);
        }

        private bool EvaluateRequirements(UpgradeNodeState state) {
            string id = state.Definition.Id;
            if (_invalidUnlockNodes.Contains(id)) return false;
            return EvaluateParents(state.Definition, _parents[id]) && EvaluateStatistics(state.Definition);
        }

        private bool EvaluateParents(UpgradeNodeDefinition definition, List<string> parents) {
            if (parents.Count == 0) return true;

            if (definition.ParentUnlockMode == ParentUnlockMode.All) {
                for (int index = 0; index < parents.Count; index++) {
                    UpgradeNodeState parent = _upgradeService.GetUpgrade(parents[index]);
                    if (parent == null || parent.Level <= 0) return false;
                }

                return true;
            }

            for (int index = 0; index < parents.Count; index++) {
                UpgradeNodeState parent = _upgradeService.GetUpgrade(parents[index]);
                if (parent != null && parent.Level > 0) return true;
            }

            return false;
        }

        private bool EvaluateStatistics(UpgradeNodeDefinition definition) {
            GameStatisticRequirement[] requirements = definition.StatisticRequirements;
            if (requirements == null || requirements.Length == 0) return true;

            if (definition.StatisticRequirementMode == StatisticRequirementMode.All) {
                for (int index = 0; index < requirements.Length; index++) {
                    if (!EvaluateStatistic(requirements[index])) return false;
                }

                return true;
            }

            for (int index = 0; index < requirements.Length; index++) {
                if (EvaluateStatistic(requirements[index])) return true;
            }

            return false;
        }

        private bool EvaluateStatistic(GameStatisticRequirement requirement) {
            if (!_statistics.TryGetValue(requirement.StatisticId, out double value)) return false;

            return requirement.Comparison switch {
                StatisticComparison.GreaterOrEqual => value >= requirement.TargetValue,
                StatisticComparison.Greater => value > requirement.TargetValue,
                StatisticComparison.Equal => value.Equals(requirement.TargetValue),
                StatisticComparison.Less => value < requirement.TargetValue,
                StatisticComparison.LessOrEqual => value <= requirement.TargetValue,
                _ => false
            };
        }

        private static bool ResolveLockedVisibility(UpgradeNodeDefinition definition) {
            return definition.LockedDisplayMode != LockedNodeDisplayMode.Hidden;
        }

        private static void WarnInvalidLink(string parentId, int index, string reason) {
            Debug.LogWarning($"Upgrade '{parentId}' child link at index {index} {reason}; the link was excluded.");
        }
    }
}
