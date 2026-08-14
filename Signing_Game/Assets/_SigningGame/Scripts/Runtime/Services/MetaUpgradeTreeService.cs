using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Data.Upgrades;
using R3;
using Services.Locator;
using UnityEngine;

namespace Services {
    public sealed class MetaUpgradeTreeConnection {
        public string ParentId { get; }
        public string ChildId { get; }
        public bool DrawEdge { get; }

        internal MetaUpgradeTreeConnection(string parentId, string childId, bool drawEdge) {
            ParentId = parentId;
            ChildId = childId;
            DrawEdge = drawEdge;
        }
    }

    public sealed class MetaUpgradeTreeService : IService, IInitialize {
        private readonly Subject<Unit> _changed = new();
        private readonly CompositeDisposable _subscriptions = new();
        private readonly List<MetaUpgradeTreeConnection> _connections = new();
        private readonly Dictionary<string, List<string>> _parents = new(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _unlocked = new(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _visible = new(StringComparer.Ordinal);
        private readonly HashSet<string> _invalid = new(StringComparer.Ordinal);
        private MetaProgressionService _meta;

        public IReadOnlyCollection<UpgradeNodeState> Nodes => _meta?.Nodes ?? Array.Empty<UpgradeNodeState>();
        public IReadOnlyList<MetaUpgradeTreeConnection> Connections => _connections;
        public Observable<Unit> Changed => _changed;

        public UniTask InitializeAsync(IServiceScope scope) {
            _meta = scope.Get<MetaProgressionService>();
            BuildGraph();
            _meta.Changed.Subscribe(_ => Reevaluate()).AddTo(_subscriptions);
            Reevaluate();
            return UniTask.CompletedTask;
        }

        public bool IsUnlocked(string id) {
            return !string.IsNullOrWhiteSpace(id) && _unlocked.TryGetValue(id, out bool value) && value;
        }

        public bool IsVisible(string id) {
            return !string.IsNullOrWhiteSpace(id) && _visible.TryGetValue(id, out bool value) && value;
        }

        public bool IsConfigurationValid(string id) {
            return !string.IsNullOrWhiteSpace(id) && !_invalid.Contains(id) && _meta?.GetUpgrade(id) != null;
        }

        public bool CanPurchase(string id) {
            return IsConfigurationValid(id) && IsUnlocked(id) && _meta.CanPurchase(id);
        }

        public void Dispose() {
            _subscriptions.Dispose();
            _changed.Dispose();
            _connections.Clear();
            _parents.Clear();
            _unlocked.Clear();
            _visible.Clear();
            _invalid.Clear();
            _meta = null;
        }

        internal void Reevaluate() {
            if (_meta == null) return;
            _unlocked.Clear();
            _visible.Clear();

            foreach (UpgradeNodeState state in _meta.Nodes) {
                string id = state.Definition.Id;
                bool unlocked = !_invalid.Contains(id) && (state.Level > 0 || EvaluateParents(id));
                bool visible = unlocked || state.Definition.LockedDisplayMode != LockedNodeDisplayMode.Hidden;
                _unlocked[id] = unlocked;
                _visible[id] = visible;
                if (state.Level == 0) {
                    state.CurrentState = unlocked ? UpgradeNodeState.State.Available : UpgradeNodeState.State.Locked;
                }
            }

            _changed.OnNext(Unit.Default);
        }

        private bool EvaluateParents(string id) {
            List<string> parents = _parents[id];
            if (parents.Count == 0) return true;
            UpgradeNodeState state = _meta.GetUpgrade(id);
            if (state.Definition.ParentUnlockMode == ParentUnlockMode.All) {
                for (int index = 0; index < parents.Count; index++) {
                    if (_invalid.Contains(parents[index]) || _meta.GetUpgrade(parents[index])?.Level <= 0) return false;
                }
                return true;
            }

            for (int index = 0; index < parents.Count; index++) {
                if (!_invalid.Contains(parents[index]) && _meta.GetUpgrade(parents[index])?.Level > 0) return true;
            }
            return false;
        }

        private void BuildGraph() {
            _connections.Clear();
            _parents.Clear();
            _invalid.Clear();
            var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (UpgradeNodeState state in _meta.Nodes) {
                string id = state.Definition.Id;
                _parents.Add(id, new List<string>());
                adjacency.Add(id, new List<string>());
                if (!Enum.IsDefined(typeof(ParentUnlockMode), state.Definition.ParentUnlockMode) ||
                    !Enum.IsDefined(typeof(LockedNodeDisplayMode), state.Definition.LockedDisplayMode)) {
                    _invalid.Add(id);
                    Debug.LogWarning($"Meta upgrade '{id}' has invalid tree settings and cannot be purchased.");
                }
            }

            foreach (UpgradeNodeState state in _meta.Nodes) {
                string parentId = state.Definition.Id;
                var linked = new HashSet<string>(StringComparer.Ordinal);
                UpgradeNodeLink[] links = state.Definition.Children;
                if (links == null) continue;
                for (int index = 0; index < links.Length; index++) {
                    UpgradeNodeDefinition childDefinition = links[index].Child;
                    if (childDefinition == null || childDefinition.GetType() != typeof(MetaUpgradeNodeDefinition) ||
                        string.IsNullOrWhiteSpace(childDefinition.Id) ||
                        _meta.GetUpgrade(childDefinition.Id) == null ||
                        string.Equals(parentId, childDefinition.Id, StringComparison.Ordinal) ||
                        !linked.Add(childDefinition.Id)) {
                        _invalid.Add(parentId);
                        Debug.LogWarning(
                            $"Meta upgrade '{parentId}' has an invalid child link at index {index} and cannot be purchased.");
                        continue;
                    }

                    string childId = childDefinition.Id;
                    adjacency[parentId].Add(childId);
                    _parents[childId].Add(parentId);
                    _connections.Add(new MetaUpgradeTreeConnection(parentId, childId, links[index].DrawEdge));
                }
            }

            MarkCycles(adjacency);
        }

        private void MarkCycles(Dictionary<string, List<string>> adjacency) {
            var colors = new Dictionary<string, byte>(StringComparer.Ordinal);
            var stack = new List<string>();
            foreach (string id in adjacency.Keys) colors[id] = 0;
            foreach (string id in adjacency.Keys) {
                if (colors[id] == 0) Visit(id, adjacency, colors, stack);
            }
        }

        private void Visit(string id, Dictionary<string, List<string>> adjacency,
            Dictionary<string, byte> colors, List<string> stack) {
            colors[id] = 1;
            stack.Add(id);
            List<string> children = adjacency[id];
            for (int index = 0; index < children.Count; index++) {
                string child = children[index];
                if (colors[child] == 0) {
                    Visit(child, adjacency, colors, stack);
                    continue;
                }
                if (colors[child] != 1) continue;
                int cycleStart = stack.LastIndexOf(child);
                for (int cycleIndex = cycleStart; cycleIndex < stack.Count; cycleIndex++) {
                    if (_invalid.Add(stack[cycleIndex])) {
                        Debug.LogWarning($"Meta upgrade '{stack[cycleIndex]}' participates in a cycle and cannot be purchased.");
                    }
                }
            }
            stack.RemoveAt(stack.Count - 1);
            colors[id] = 2;
        }
    }
}
