using System;
using R3;
using UnityEngine;
using UnityEngine.UIElements;
using Views.Models;

namespace Views {
    public class UpgradeTile : IDisposable {
        private readonly VisualElement _root;
        private readonly VisualElement _icon;
        private readonly Label _level;

        private CompositeDisposable _disposable = new();
        private UpgradeTileViewModel _viewModel;
        private Action<string> _clicked;
        private string _stateClass;

        public VisualElement Root => _root;

        public UpgradeTile() {
            _root = new VisualElement();
            _root.AddToClassList("upgrade-node");
            _root.RegisterCallback<ClickEvent>(OnClicked);

            _icon = new VisualElement();
            _icon.AddToClassList("upgrade-node__icon");

            _level = new Label();
            _level.AddToClassList("upgrade-node__level");

            _root.Add(_icon);
            _root.Add(_level);
        }

        public void Bind(UpgradeTileViewModel viewModel, Action<string> clicked) {
            _disposable.Dispose();
            _disposable = new CompositeDisposable();

            _viewModel = viewModel;
            _clicked = clicked;

            _root.style.left = viewModel.Position.x;
            _root.style.top = viewModel.Position.y;
            _root.style.backgroundColor = viewModel.UpgradeColor;
            _icon.style.backgroundColor = Color.clear;
            SetBackground(viewModel.Icon);

            viewModel.State
                .Subscribe(SetState)
                .AddTo(_disposable);

            viewModel.Level
                .Subscribe(_ => RefreshLevel())
                .AddTo(_disposable);
        }

        public void SetSelected(bool selected) {
            if (selected) {
                _root.AddToClassList("upgrade-node--selected");
            } else {
                _root.RemoveFromClassList("upgrade-node--selected");
            }
        }

        public void Dispose() {
            _disposable.Dispose();
            _root.UnregisterCallback<ClickEvent>(OnClicked);
        }

        private void OnClicked(ClickEvent evt) {
            if (_viewModel == null) {
                return;
            }

            _clicked?.Invoke(_viewModel.Id);
            evt.StopPropagation();
        }

        private void SetState(UpgradeNodeVisualState state) {
            if (!string.IsNullOrWhiteSpace(_stateClass)) {
                _root.RemoveFromClassList(_stateClass);
            }

            _stateClass = GetNodeStateClass(state);
            _root.AddToClassList(_stateClass);
            ApplyBorder(state);
        }

        private void RefreshLevel() {
            _level.text = $"{_viewModel.Level.Value}/{_viewModel.MaxLevel}";
        }

        private void SetBackground(UnityEngine.Sprite sprite) {
            if (sprite == null) {
                _icon.style.backgroundImage = new StyleBackground();
                return;
            }

            _icon.style.backgroundImage = new StyleBackground(sprite);
        }

        private void ApplyBorder(UpgradeNodeVisualState state) {
            if (state == UpgradeNodeVisualState.Completed) {
                SetBorderWidth(0f);
                return;
            }

            SetBorderWidth(2f);

            var borderColor = state == UpgradeNodeVisualState.Available
                ? new Color(86f / 255f, 184f / 255f, 122f / 255f, 1f)
                : GetReadableBorderColor(_viewModel.UpgradeColor);

            SetBorderColor(borderColor);
        }

        private void SetBorderWidth(float width) {
            _root.style.borderTopWidth = width;
            _root.style.borderRightWidth = width;
            _root.style.borderBottomWidth = width;
            _root.style.borderLeftWidth = width;
        }

        private void SetBorderColor(Color color) {
            _root.style.borderTopColor = color;
            _root.style.borderRightColor = color;
            _root.style.borderBottomColor = color;
            _root.style.borderLeftColor = color;
        }

        private static Color GetReadableBorderColor(Color backgroundColor) {
            var luminance = backgroundColor.r * 0.2126f + backgroundColor.g * 0.7152f + backgroundColor.b * 0.0722f;
            var target = luminance < 0.5f ? Color.white : Color.black;
            var color = Color.Lerp(backgroundColor, target, 0.35f);
            color.a = 1f;
            return color;
        }

        private static string GetNodeStateClass(UpgradeNodeVisualState state) {
            return state switch {
                UpgradeNodeVisualState.Locked => "upgrade-node--locked",
                UpgradeNodeVisualState.Available => "upgrade-node--available",
                UpgradeNodeVisualState.InProgress => "upgrade-node--inprogress",
                UpgradeNodeVisualState.Completed => "upgrade-node--completed",
                _ => "upgrade-node--locked"
            };
        }
    }
}
