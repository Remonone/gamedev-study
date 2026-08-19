using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using Services;
using Services.Locator;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Presentation {
    public sealed class MetaUpgradeTreeView : MonoBehaviour {
        [SerializeField] private PullTabView _pullTab;
        [SerializeField] private RectTransform _content;
        [SerializeField] private UpgradeEdgeGraphic _edgeGraphic;
        [SerializeField] private UpgradeNodeView _nodePrefab;
        [SerializeField] private UpgradeDetailsView _detailsView;
        [SerializeField] private MetaPurchaseConfirmationView _confirmation;
        [SerializeField] private TextMeshProUGUI _currencyText;
        [SerializeField] private TextMeshProUGUI _iterationText;
        [SerializeField] private Button _confirmSelectionButton;

        private readonly List<UpgradeNodeView> _nodeViews = new();
        private readonly CompositeDisposable _subscriptions = new();
        private MetaUpgradeTreeViewModel _viewModel;
        private UnityAction _confirmSelectionAction;

        private async void Start() {
            if (!ValidateReferences()) { enabled = false; return; }
            ServiceLocator locator = ServiceLocator.For(this);
            try {
                await UniTask.WaitUntil(() => locator != null && locator.IsReady,
                    cancellationToken: this.GetCancellationTokenOnDestroy());
            } catch (OperationCanceledException) { return; }
            if (this == null) return;

            _viewModel = new MetaUpgradeTreeViewModel(
                locator.Get<MetaProgressionService>(),
                locator.Get<MetaUpgradeTreeService>(),
                locator.Get<MetaPurchaseService>());
            _viewModel.Changed.Subscribe(_ => Refresh()).AddTo(_subscriptions);
            _viewModel.SelectedNode.Subscribe(OnSelected).AddTo(_subscriptions);
            _pullTab.BindAvailability(_viewModel.Availability);
            _confirmSelectionAction = ConfirmSelection;
            _confirmSelectionButton.onClick.AddListener(_confirmSelectionAction);
            Refresh();
        }

        private void Refresh() {
            if (_viewModel == null) return;
            ClearNodes();
            _edgeGraphic.transform.SetAsFirstSibling();
            var rects = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            for (int index = 0; index < _viewModel.Nodes.Count; index++) {
                UpgradeNodePresentationModel node = _viewModel.Nodes[index];
                if (!node.IsVisible) continue;
                UpgradeNodeView view = Instantiate(_nodePrefab, _content, false);
                RectTransform rect = (RectTransform)view.transform;
                rect.anchoredPosition = node.Position;
                view.Bind(node, _viewModel.SelectNode);
                _nodeViews.Add(view);
                rects.Add(node.Id, rect);
            }

            var positioned = new List<UpgradeEdgePresentationModel>(_viewModel.Edges.Count);
            RectTransform edgeRect = _edgeGraphic.rectTransform;
            for (int index = 0; index < _viewModel.Edges.Count; index++) {
                UpgradeEdgePresentationModel edge = _viewModel.Edges[index];
                if (!rects.TryGetValue(edge.ParentId, out RectTransform parent) ||
                    !rects.TryGetValue(edge.ChildId, out RectTransform child)) continue;
                positioned.Add(new UpgradeEdgePresentationModel(edge.ParentId, edge.ChildId,
                    edgeRect.InverseTransformPoint(parent.TransformPoint(parent.rect.center)),
                    edgeRect.InverseTransformPoint(child.TransformPoint(child.rect.center))));
            }
            _edgeGraphic.SetEdges(positioned);
            _currencyText.text = $"Banked: {_viewModel.BankedPointsText}   Available: {_viewModel.AvailablePointsText} (-{_viewModel.SpentPointsText})";
            _iterationText.text = $"This iteration: {_viewModel.CurrentPointsText}   Previous: {_viewModel.PreviousPointsText}";
            _confirmSelectionButton.gameObject.SetActive(_viewModel.CanConfirm);
            _confirmSelectionButton.interactable = _viewModel.CanConfirm;
        }

        private void OnSelected(UpgradeNodePresentationModel node) {
            if (node == null) _detailsView.Hide();
            else _detailsView.Show(node, RequestPurchase, _viewModel.ClearSelection);
        }

        private bool RequestPurchase(string id) {
            UpgradeNodePresentationModel selected = null;
            for (int index = 0; index < _viewModel.Nodes.Count; index++) {
                if (string.Equals(_viewModel.Nodes[index].Id, id, StringComparison.Ordinal)) {
                    selected = _viewModel.Nodes[index];
                    break;
                }
            }
            if (selected == null || !selected.CanPurchase) return false;
            return _viewModel.Purchase(id);
        }

        private void ConfirmSelection() {
            if (_viewModel == null || !_viewModel.CanConfirm) return;
            _confirmation.Show(_viewModel.SpentPointsText, () => _viewModel.ConfirmSelection());
        }

        private void ClearNodes() {
            for (int index = 0; index < _nodeViews.Count; index++) {
                if (_nodeViews[index] == null) continue;
                _nodeViews[index].Unbind();
                Destroy(_nodeViews[index].gameObject);
            }
            _nodeViews.Clear();
        }

        private bool ValidateReferences() {
            bool valid = _pullTab != null && _content != null && _edgeGraphic != null && _nodePrefab != null &&
                         _detailsView != null && _confirmation != null && _currencyText != null &&
                         _iterationText != null && _confirmSelectionButton != null &&
                         _edgeGraphic.transform.parent == _content;
            if (!valid) Debug.LogError("MetaUpgradeTreeView requires all tree, tab, labels and confirmation references.", this);
            return valid;
        }

        private void OnDestroy() {
            _pullTab?.UnbindAvailability();
            if (_confirmSelectionButton != null && _confirmSelectionAction != null) {
                _confirmSelectionButton.onClick.RemoveListener(_confirmSelectionAction);
            }
            _subscriptions.Dispose();
            ClearNodes();
            _viewModel?.Dispose();
            _viewModel = null;
        }
    }
}
