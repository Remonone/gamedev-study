using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using UnityEngine.UIElements;
using Views.Models;

namespace Views {
    public class UpgradesTabView : IDisposable {
        private const float NodeWidth = 32f;
        private const float NodeHeight = 36f;

        private readonly VisualElement _root;
        private readonly VisualElement _workspace;
        private readonly VisualElement _treeWindow;
        private readonly VisualElement _treeHeader;
        private readonly VisualElement _treeViewport;
        private readonly VisualElement _treeCanvas;
        private readonly VisualElement _connectionsLayer;
        private readonly VisualElement _nodesLayer;

        private readonly VisualElement _selectedIcon;
        private readonly Label _selectedName;
        private readonly Label _selectedDescription;
        private readonly Label _selectedLevel;
        private readonly Label _selectedPrice;
        private readonly Button _upgradeButton;

        private readonly Dictionary<string, UpgradeTileViewModel> _tilesById = new();
        private readonly Dictionary<string, UpgradeTile> _tileViewsById = new();

        private CompositeDisposable _disposable = new();
        private CompositeDisposable _selectedTileDisposable = new();
        private UpgradesTabViewModel _viewModel;

        private bool _isWindowDragging;
        private int _windowDragPointerId = -1;
        private Vector2 _windowDragStartPointerPosition;
        private Vector2 _windowDragStartOffset;

        private bool _isCanvasDragging;
        private int _canvasDragPointerId = -1;
        private Vector2 _canvasDragStartPointerPosition;
        private Vector2 _canvasDragStartOffset;
        private Vector2 _canvasOffset;

        public UpgradesTabView(VisualElement root) {
            _root = root;
            _workspace = _root.Q<VisualElement>("UpgradesWorkspace");
            _treeWindow = _root.Q<VisualElement>("UpgradeTreeWindow");
            _treeHeader = _root.Q<VisualElement>("UpgradeTreeHeader");
            _treeViewport = _root.Q<VisualElement>("UpgradeTreeViewport");
            _treeCanvas = _root.Q<VisualElement>("UpgradeTreeCanvas");
            _connectionsLayer = _root.Q<VisualElement>("UpgradeConnectionsLayer");
            _nodesLayer = _root.Q<VisualElement>("UpgradeNodesLayer");

            _selectedIcon = _root.Q<VisualElement>("SelectedUpgradeIcon");
            _selectedName = _root.Q<Label>("SelectedUpgradeName");
            _selectedDescription = _root.Q<Label>("SelectedUpgradeDescription");
            _selectedLevel = _root.Q<Label>("SelectedUpgradeLevel");
            _selectedPrice = _root.Q<Label>("SelectedUpgradePrice");
            _upgradeButton = _root.Q<Button>("UpgradeNodeButton");

            _upgradeButton.clicked += OnUpgradeClicked;

            _treeHeader.RegisterCallback<PointerDownEvent>(OnWindowDragPointerDown);
            _treeHeader.RegisterCallback<PointerMoveEvent>(OnWindowDragPointerMove);
            _treeHeader.RegisterCallback<PointerUpEvent>(OnWindowDragPointerUp);
            _treeHeader.RegisterCallback<PointerCaptureOutEvent>(OnWindowDragPointerCaptureOut);

            _treeViewport.RegisterCallback<PointerDownEvent>(OnCanvasDragPointerDown);
            _treeViewport.RegisterCallback<PointerMoveEvent>(OnCanvasDragPointerMove);
            _treeViewport.RegisterCallback<PointerUpEvent>(OnCanvasDragPointerUp);
            _treeViewport.RegisterCallback<PointerCaptureOutEvent>(OnCanvasDragPointerCaptureOut);

            _connectionsLayer.generateVisualContent += OnDrawConnections;

            ResetDetails();
        }

        public void Bind(UpgradesTabViewModel viewModel) {
            _disposable.Dispose();
            _disposable = new CompositeDisposable();
            _selectedTileDisposable.Dispose();
            _selectedTileDisposable = new CompositeDisposable();

            ClearTree();

            _viewModel = viewModel;
            if (_viewModel == null) {
                ResetDetails();
                return;
            }

            _viewModel.RequestInitialState();
            BuildTree(_viewModel.Tiles);

            _viewModel.SelectedNodeId
                .Subscribe(_ => ApplySelection())
                .AddTo(_disposable);

            _viewModel.SelectedTile
                .Subscribe(BindSelectedTile)
                .AddTo(_disposable);
        }

        public void Dispose() {
            _disposable.Dispose();
            _selectedTileDisposable.Dispose();
            ClearTree();
            _connectionsLayer.generateVisualContent -= OnDrawConnections;
            _upgradeButton.clicked -= OnUpgradeClicked;
        }

        private void OnUpgradeClicked() {
            _viewModel?.UpgradeSelectedNode();
        }

        private void OnWindowDragPointerDown(PointerDownEvent evt) {
            if (evt.button != (int)MouseButton.LeftMouse) {
                return;
            }

            _isWindowDragging = true;
            _windowDragPointerId = evt.pointerId;
            _windowDragStartPointerPosition = GetPointerPosition(evt.position.x, evt.position.y);
            _windowDragStartOffset = new Vector2(_treeWindow.resolvedStyle.left, _treeWindow.resolvedStyle.top);
            _treeHeader.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnWindowDragPointerMove(PointerMoveEvent evt) {
            if (!_isWindowDragging || evt.pointerId != _windowDragPointerId) {
                return;
            }

            var delta = GetPointerPosition(evt.position.x, evt.position.y) - _windowDragStartPointerPosition;
            var target = _windowDragStartOffset + delta;
            var clamped = ClampWindowPosition(target);

            _treeWindow.style.left = clamped.x;
            _treeWindow.style.top = clamped.y;
            evt.StopPropagation();
        }

        private void OnWindowDragPointerUp(PointerUpEvent evt) {
            if (!_isWindowDragging || evt.pointerId != _windowDragPointerId) {
                return;
            }

            StopWindowDragging(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnWindowDragPointerCaptureOut(PointerCaptureOutEvent evt) {
            if (evt.pointerId != _windowDragPointerId) {
                return;
            }

            StopWindowDragging(evt.pointerId);
        }

        private void StopWindowDragging(int pointerId) {
            _isWindowDragging = false;
            _windowDragPointerId = -1;

            if (_treeHeader.HasPointerCapture(pointerId)) {
                _treeHeader.ReleasePointer(pointerId);
            }
        }

        private Vector2 ClampWindowPosition(Vector2 target) {
            if (_workspace.layout.width <= 0f || _workspace.layout.height <= 0f) {
                return target;
            }

            var maxX = Mathf.Max(0f, _workspace.layout.width - _treeWindow.layout.width);
            var maxY = Mathf.Max(0f, _workspace.layout.height - _treeWindow.layout.height);

            return new Vector2(
                Mathf.Clamp(target.x, 0f, maxX),
                Mathf.Clamp(target.y, 0f, maxY)
            );
        }

        private void OnCanvasDragPointerDown(PointerDownEvent evt) {
            if (evt.button != (int)MouseButton.LeftMouse) {
                return;
            }

            if (IsInNodeHierarchy(evt.target as VisualElement)) {
                return;
            }

            _isCanvasDragging = true;
            _canvasDragPointerId = evt.pointerId;
            _canvasDragStartPointerPosition = GetPointerPosition(evt.position.x, evt.position.y);
            _canvasDragStartOffset = _canvasOffset;
            _treeViewport.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnCanvasDragPointerMove(PointerMoveEvent evt) {
            if (!_isCanvasDragging || evt.pointerId != _canvasDragPointerId) {
                return;
            }

            var delta = GetPointerPosition(evt.position.x, evt.position.y) - _canvasDragStartPointerPosition;
            var target = _canvasDragStartOffset + delta;
            _canvasOffset = ClampCanvasPosition(target);
            ApplyCanvasOffset();
            evt.StopPropagation();
        }

        private void OnCanvasDragPointerUp(PointerUpEvent evt) {
            if (!_isCanvasDragging || evt.pointerId != _canvasDragPointerId) {
                return;
            }

            StopCanvasDragging(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnCanvasDragPointerCaptureOut(PointerCaptureOutEvent evt) {
            if (evt.pointerId != _canvasDragPointerId) {
                return;
            }

            StopCanvasDragging(evt.pointerId);
        }

        private void StopCanvasDragging(int pointerId) {
            _isCanvasDragging = false;
            _canvasDragPointerId = -1;

            if (_treeViewport.HasPointerCapture(pointerId)) {
                _treeViewport.ReleasePointer(pointerId);
            }
        }

        private Vector2 ClampCanvasPosition(Vector2 target) {
            if (_treeViewport.layout.width <= 0f || _treeViewport.layout.height <= 0f) {
                return target;
            }

            var minX = _treeViewport.layout.width - _treeCanvas.layout.width;
            var minY = _treeViewport.layout.height - _treeCanvas.layout.height;

            if (minX > 0f) {
                minX = 0f;
            }

            if (minY > 0f) {
                minY = 0f;
            }

            return new Vector2(
                Mathf.Clamp(target.x, minX, 0f),
                Mathf.Clamp(target.y, minY, 0f)
            );
        }

        private void ApplyCanvasOffset() {
            _treeCanvas.style.left = _canvasOffset.x;
            _treeCanvas.style.top = _canvasOffset.y;
        }

        private void BuildTree(IReadOnlyList<UpgradeTileViewModel> tiles) {
            if (tiles == null) {
                return;
            }

            foreach (var tileViewModel in tiles) {
                if (tileViewModel == null || string.IsNullOrWhiteSpace(tileViewModel.Id)) {
                    continue;
                }

                _tilesById[tileViewModel.Id] = tileViewModel;

                var tileView = new UpgradeTile();
                tileView.Bind(tileViewModel, _viewModel.SelectNode);
                _tileViewsById[tileViewModel.Id] = tileView;
                _nodesLayer.Add(tileView.Root);
            }

            ApplySelection();
            _connectionsLayer.MarkDirtyRepaint();
        }

        private void ClearTree() {
            foreach (var tileView in _tileViewsById.Values) {
                tileView.Dispose();
            }

            _tileViewsById.Clear();
            _tilesById.Clear();
            _nodesLayer.Clear();
        }

        private void ApplySelection() {
            var selectedId = _viewModel == null ? string.Empty : _viewModel.SelectedNodeId.Value;

            foreach (var pair in _tileViewsById) {
                pair.Value.SetSelected(pair.Key == selectedId);
            }
        }

        private void BindSelectedTile(UpgradeTileViewModel tile) {
            _selectedTileDisposable.Dispose();
            _selectedTileDisposable = new CompositeDisposable();

            if (tile == null) {
                ResetDetails();
                return;
            }

            _selectedName.text = string.IsNullOrWhiteSpace(tile.Name) ? tile.Id : tile.Name;
            _selectedDescription.text = string.IsNullOrWhiteSpace(tile.Description) ? "-" : tile.Description;
            SetBackground(_selectedIcon, tile.Icon);

            tile.State
                .Subscribe(_ => RefreshDetailsPanel(tile))
                .AddTo(_selectedTileDisposable);

            tile.Level
                .Subscribe(_ => RefreshDetailsPanel(tile))
                .AddTo(_selectedTileDisposable);

            tile.CanUpgrade
                .Subscribe(_ => RefreshUpgradeButtonState(tile))
                .AddTo(_selectedTileDisposable);
            
            tile.Price
                .Subscribe(_ => RefreshDetailsPanel(tile))
                .AddTo(_selectedTileDisposable);
        }

        private void RefreshDetailsPanel(UpgradeTileViewModel tile) {
            _selectedLevel.text = $"Level: {tile.Level.Value}/{tile.MaxLevel}";
            _selectedPrice.text = $"Price: {tile.Price.Value}";
            
            _upgradeButton.style.display = tile.Level.Value < tile.MaxLevel ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshUpgradeButtonState(tile);
        }

        private void ResetDetails() {
            _selectedName.text = "Select a node";
            _selectedDescription.text = "Choose a node in the tree to inspect details.";
            _selectedLevel.text = "Level: -";
            _selectedPrice.text = "Price: -";
            SetBackground(_selectedIcon, null);
            _upgradeButton.style.display = DisplayStyle.None;
            _upgradeButton.SetEnabled(false);
        }

        private void RefreshUpgradeButtonState(UpgradeTileViewModel tile) {
            var canUpgrade = tile != null && tile.CanUpgrade.Value;
            _upgradeButton.SetEnabled(canUpgrade);
        }

        private void OnDrawConnections(MeshGenerationContext context) {
            if (_tilesById.Count == 0) {
                return;
            }

            var painter = context.painter2D;
            painter.lineWidth = 2f;
            painter.strokeColor = new Color(0.25f, 0.38f, 0.53f, 0.85f);

            foreach (var tile in _tilesById.Values) {
                if (tile.ChildIds == null) {
                    continue;
                }

                foreach (var childId in tile.ChildIds) {
                    if (string.IsNullOrWhiteSpace(childId) || !_tilesById.TryGetValue(childId, out var child)) {
                        continue;
                    }

                    var start = new Vector2(tile.Position.x + NodeWidth, tile.Position.y + NodeHeight * 0.5f);
                    var end = new Vector2(child.Position.x, child.Position.y + NodeHeight * 0.5f);

                    painter.BeginPath();
                    painter.MoveTo(start);
                    painter.LineTo(end);
                    painter.Stroke();
                }
            }
        }

        private static void SetBackground(VisualElement element, Sprite sprite) {
            if (element == null) {
                return;
            }

            if (sprite == null) {
                element.style.backgroundImage = new StyleBackground();
                return;
            }

            element.style.backgroundImage = new StyleBackground(sprite);
        }

        private static string GetStateText(UpgradeNodeVisualState state) {
            return state switch {
                UpgradeNodeVisualState.Locked => "Locked",
                UpgradeNodeVisualState.Available => "Available",
                UpgradeNodeVisualState.InProgress => "In progress",
                UpgradeNodeVisualState.Completed => "Completed",
                _ => "Locked"
            };
        }

        private static bool IsInNodeHierarchy(VisualElement target) {
            while (target != null) {
                if (target.ClassListContains("upgrade-node")) {
                    return true;
                }

                target = target.parent;
            }

            return false;
        }

        private static Vector2 GetPointerPosition(float x, float y) {
            return new Vector2(x, y);
        }
    }
}
