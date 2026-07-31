using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using Services;
using Services.Locator;
using UI;
using UnityEngine;

namespace Presentation {
    public sealed class UpgradeTreeView : MonoBehaviour {
        [SerializeField] private RectTransform _content;
        [SerializeField] private UpgradeEdgeGraphic _edgeGraphic;
        [SerializeField] private UpgradeNodeView _nodePrefab;
        [SerializeField] private UpgradeDetailsView _detailsView;

        private readonly List<UpgradeNodeView> _nodeViews = new();
        private readonly CompositeDisposable _viewSubscriptions = new();
        private UpgradeTreeViewModel _viewModel;

        private async void Start() {
            if (!ValidateReferences()) {
                enabled = false;
                return;
            }

            ServiceLocator locator = ServiceLocator.For(this);
            try {
                await UniTask.WaitUntil(() => locator != null && locator.IsReady,
                    cancellationToken: this.GetCancellationTokenOnDestroy());
            } catch (OperationCanceledException) {
                return;
            }

            if (this == null) return;
            _viewModel = new UpgradeTreeViewModel(locator.Get<UpgradeTreeService>());
            _viewModel.Changed.Subscribe(_ => RefreshNodes()).AddTo(_viewSubscriptions);
            _viewModel.SelectedNode.Subscribe(OnSelectedNodeChanged).AddTo(_viewSubscriptions);
            RefreshNodes();
        }

        private void RefreshNodes() {
            ClearNodeBindings();
            _edgeGraphic.transform.SetAsFirstSibling();
            var nodeRects = new Dictionary<string, RectTransform>(StringComparer.Ordinal);

            IReadOnlyList<UpgradeNodePresentationModel> nodes = _viewModel.Nodes;
            for (int index = 0; index < nodes.Count; index++) {
                UpgradeNodePresentationModel node = nodes[index];
                if (!node.IsVisible) continue;

                UpgradeNodeView nodeView = Instantiate(_nodePrefab, _content, false);
                RectTransform nodeRect = (RectTransform)nodeView.transform;
                nodeRect.anchoredPosition = node.Position;
                nodeView.Bind(node, _viewModel.SelectNode);
                _nodeViews.Add(nodeView);
                nodeRects.Add(node.Id, nodeRect);
            }

            IReadOnlyList<UpgradeEdgePresentationModel> sourceEdges = _viewModel.Edges;
            var positionedEdges = new List<UpgradeEdgePresentationModel>(sourceEdges.Count);
            RectTransform edgeRect = _edgeGraphic.rectTransform;
            for (int index = 0; index < sourceEdges.Count; index++) {
                UpgradeEdgePresentationModel edge = sourceEdges[index];
                if (!nodeRects.TryGetValue(edge.ParentId, out RectTransform parent) ||
                    !nodeRects.TryGetValue(edge.ChildId, out RectTransform child)) continue;

                Vector2 start = edgeRect.InverseTransformPoint(parent.TransformPoint(parent.rect.center));
                Vector2 end = edgeRect.InverseTransformPoint(child.TransformPoint(child.rect.center));
                positionedEdges.Add(new UpgradeEdgePresentationModel(
                    edge.ParentId, edge.ChildId, start, end));
            }

            _edgeGraphic.SetEdges(positionedEdges);
        }

        private void OnSelectedNodeChanged(UpgradeNodePresentationModel node) {
            if (node == null) _detailsView.Hide();
            else _detailsView.Show(node);
        }

        private void OnDestroy() {
            _viewSubscriptions.Dispose();
            ClearNodeBindings();
            _viewModel?.Dispose();
            _viewModel = null;
        }

        private void ClearNodeBindings() {
            for (int index = 0; index < _nodeViews.Count; index++) {
                UpgradeNodeView nodeView = _nodeViews[index];
                if (nodeView == null) continue;
                nodeView.Unbind();
                Destroy(nodeView.gameObject);
            }

            _nodeViews.Clear();
        }

        private bool ValidateReferences() {
            if (_content != null && _edgeGraphic != null && _nodePrefab != null && _detailsView != null &&
                _edgeGraphic.transform.parent == _content) return true;
            Debug.LogError("UpgradeTreeView requires content, edge graphic, node prefab, and details view references.", this);
            return false;
        }
    }
}
