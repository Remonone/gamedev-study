using System;
using System.Collections.Generic;
using Data.Upgrades;
using R3;
using Services;

namespace Presentation {
    public sealed class UpgradeTreeViewModel : IDisposable {
        private readonly UpgradeTreeService _treeService;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly Subject<Unit> _changed = new();
        private readonly ReactiveProperty<UpgradeNodePresentationModel> _selectedNode = new();
        private readonly List<UpgradeNodePresentationModel> _nodes = new();
        private readonly List<UpgradeEdgePresentationModel> _edges = new();

        public IReadOnlyList<UpgradeNodePresentationModel> Nodes => _nodes;
        public IReadOnlyList<UpgradeEdgePresentationModel> Edges => _edges;
        public Observable<Unit> Changed => _changed;
        public Observable<UpgradeNodePresentationModel> SelectedNode => _selectedNode;

        public UpgradeTreeViewModel(UpgradeTreeService treeService) {
            _treeService = treeService ?? throw new ArgumentNullException(nameof(treeService));
            _treeService.Changed.Subscribe(_ => Rebuild()).AddTo(_subscriptions);
            Rebuild();
        }

        public void SelectNode(string upgradeId) {
            if (string.IsNullOrWhiteSpace(upgradeId)) return;
            for (int index = 0; index < _nodes.Count; index++) {
                UpgradeNodePresentationModel node = _nodes[index];
                if (node.IsVisible && string.Equals(node.Id, upgradeId, StringComparison.Ordinal)) {
                    _selectedNode.Value = node;
                    return;
                }
            }
        }

        public void Dispose() {
            _subscriptions.Dispose();
            _selectedNode.Dispose();
            _changed.Dispose();
            _nodes.Clear();
            _edges.Clear();
        }

        private void Rebuild() {
            string selectedId = _selectedNode.CurrentValue?.Id;
            _nodes.Clear();
            _edges.Clear();

            var byId = new Dictionary<string, UpgradeNodePresentationModel>(StringComparer.Ordinal);
            foreach (UpgradeNodeState state in _treeService.Nodes) {
                bool completed = state.Level >= state.Definition.MaxLevel;
                string price = completed ? "MAX" : _treeService.ResolvePrice(state).ToString();
                var node = new UpgradeNodePresentationModel(
                    state.Definition.Id,
                    state.Definition.Name,
                    state.Definition.Description,
                    state.Definition.Icon,
                    state.Definition.TreePosition,
                    state.Level,
                    state.Definition.MaxLevel,
                    price,
                    _treeService.IsUnlocked(state.Definition.Id),
                    _treeService.IsVisible(state.Definition.Id));
                _nodes.Add(node);
                byId.Add(node.Id, node);
            }

            IReadOnlyList<UpgradeTreeConnection> connections = _treeService.Connections;
            for (int index = 0; index < connections.Count; index++) {
                UpgradeTreeConnection connection = connections[index];
                if (!connection.DrawEdge ||
                    !byId.TryGetValue(connection.Parent.Definition.Id, out UpgradeNodePresentationModel parent) ||
                    !byId.TryGetValue(connection.Child.Definition.Id, out UpgradeNodePresentationModel child) ||
                    !parent.IsVisible || !child.IsVisible) continue;

                _edges.Add(new UpgradeEdgePresentationModel(
                    parent.Id, child.Id, parent.Position, child.Position));
            }

            UpgradeNodePresentationModel selected = null;
            if (selectedId != null && byId.TryGetValue(selectedId, out UpgradeNodePresentationModel candidate) &&
                candidate.IsVisible) {
                selected = candidate;
            }

            _selectedNode.Value = selected;
            _changed.OnNext(Unit.Default);
        }
    }
}
