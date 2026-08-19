using System;
using System.Collections.Generic;
using Data.Upgrades;
using R3;
using Services;

namespace Presentation {
    public sealed class MetaUpgradeTreeViewModel : IDisposable {
        private readonly MetaProgressionService _meta;
        private readonly MetaUpgradeTreeService _tree;
        private readonly MetaPurchaseService _purchases;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly Subject<Unit> _changed = new();
        private readonly ReactiveProperty<bool> _availability = new(false);
        private readonly ReactiveProperty<UpgradeNodePresentationModel> _selected = new();
        private readonly List<UpgradeNodePresentationModel> _nodes = new();
        private readonly List<UpgradeEdgePresentationModel> _edges = new();

        public IReadOnlyList<UpgradeNodePresentationModel> Nodes => _nodes;
        public IReadOnlyList<UpgradeEdgePresentationModel> Edges => _edges;
        public Observable<Unit> Changed => _changed;
        public Observable<bool> Availability => _availability;
        public Observable<UpgradeNodePresentationModel> SelectedNode => _selected;
        public string CurrentPointsText => _meta.CurrentIterationPoints.ToString();
        public string PreviousPointsText => _meta.PreviousIterationPoints.ToString();
        public string BankedPointsText => _meta.BankedPoints.ToString();
        public string AvailablePointsText => _meta.PreviewAvailablePoints.ToString();
        public string SpentPointsText => _meta.SpentPoints.ToString();
        public bool CanConfirm => _purchases.CanConfirm;

        public MetaUpgradeTreeViewModel(MetaProgressionService meta, MetaUpgradeTreeService tree,
            MetaPurchaseService purchases) {
            _meta = meta ?? throw new ArgumentNullException(nameof(meta));
            _tree = tree ?? throw new ArgumentNullException(nameof(tree));
            _purchases = purchases ?? throw new ArgumentNullException(nameof(purchases));
            _tree.Changed.Subscribe(_ => Rebuild()).AddTo(_subscriptions);
            _meta.Changed.Subscribe(_ => Rebuild()).AddTo(_subscriptions);
            Rebuild();
        }

        public void SelectNode(string id) {
            for (int index = 0; index < _nodes.Count; index++) {
                UpgradeNodePresentationModel node = _nodes[index];
                if (node.IsVisible && string.Equals(node.Id, id, StringComparison.Ordinal)) {
                    _selected.Value = node;
                    return;
                }
            }
        }

        public void ClearSelection() => _selected.Value = null;

        public bool Purchase(string id) => _purchases.TryStagePurchase(id);

        public bool ConfirmSelection() => _purchases.TryConfirmSelection();

        public void Dispose() {
            _subscriptions.Dispose();
            _availability.Dispose();
            _selected.Dispose();
            _changed.Dispose();
            _nodes.Clear();
            _edges.Clear();
        }

        private void Rebuild() {
            string selectedId = _selected.CurrentValue?.Id;
            _nodes.Clear();
            _edges.Clear();
            var byId = new Dictionary<string, UpgradeNodePresentationModel>(StringComparer.Ordinal);

            foreach (UpgradeNodeState state in _tree.Nodes) {
                int effectiveLevel = _meta.EffectiveLevel(state.Definition.Id);
                bool completed = state.Definition.IsTerminalLevel(effectiveLevel);
                string price = completed ? "MAX" :
                    _meta.TryResolveCost(state, out long cost) ? cost.ToString() : "INVALID";
                string level = state.Definition.HasLevelCap
                    ? $"{effectiveLevel}/{state.Definition.MaxLevel}"
                    : $"{effectiveLevel}/∞";
                var node = new UpgradeNodePresentationModel(
                    state.Definition.Id,
                    state.Definition.Name,
                    UpgradeDescriptionFormatter.Format(state.Definition, effectiveLevel),
                    state.Definition.Icon,
                    UpgradeNodePresentationModel.ToRuntimePosition(state.Definition.TreePosition),
                    effectiveLevel,
                    state.Definition.MaxLevel,
                    level,
                    price,
                    _tree.IsUnlocked(state.Definition.Id),
                    _tree.IsVisible(state.Definition.Id),
                    _meta.IsPreviewed(state.Definition.Id),
                    1f,
                    _tree.CanPurchase(state.Definition.Id));
                _nodes.Add(node);
                byId.Add(node.Id, node);
            }

            IReadOnlyList<MetaUpgradeTreeConnection> connections = _tree.Connections;
            for (int index = 0; index < connections.Count; index++) {
                MetaUpgradeTreeConnection connection = connections[index];
                if (!connection.DrawEdge || !byId.TryGetValue(connection.ParentId, out UpgradeNodePresentationModel parent) ||
                    !byId.TryGetValue(connection.ChildId, out UpgradeNodePresentationModel child) ||
                    !parent.IsVisible || !child.IsVisible) continue;
                _edges.Add(new UpgradeEdgePresentationModel(parent.Id, child.Id, parent.Position, child.Position));
            }

            _availability.Value = _meta.IsEligible;
            _selected.Value = selectedId != null && byId.TryGetValue(selectedId, out UpgradeNodePresentationModel selected) &&
                              selected.IsVisible ? selected : null;
            _changed.OnNext(Unit.Default);
        }
    }
}
