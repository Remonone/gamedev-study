using R3;
using UnityEngine.UIElements;
using Views.Models;

namespace Views {
    public sealed class GlobalEventIndicatorView {
        private readonly VisualElement _root;
        private readonly VisualElement _icon;
        private readonly VisualElement _tooltip;
        private readonly Label _tooltipTitle;
        private readonly Label _tooltipDescription;
        private readonly CompositeDisposable _disposable = new();

        public GlobalEventIndicatorView(VisualElement root) {
            _root = root;
            _icon = root?.Q<VisualElement>("GlobalEventIcon");
            _tooltip = root?.Q<VisualElement>("GlobalEventTooltip");
            _tooltipTitle = root?.Q<Label>("GlobalEventTooltipTitle");
            _tooltipDescription = root?.Q<Label>("GlobalEventTooltipDescription");
        }

        public void Bind(GlobalEventIndicatorViewModel viewModel) {
            if (_root == null || viewModel == null) return;

            _root.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            _root.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            HideTooltip();

            viewModel.CurrentEvent
                .Subscribe(UpdateEvent)
                .AddTo(_disposable);
        }

        public void Dispose() {
            if (_root != null) {
                _root.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
                _root.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
            }

            _disposable.Dispose();
        }

        private void UpdateEvent(Types.Events.Global.GlobalEvent globalEvent) {
            if (globalEvent == null) {
                _root.style.display = DisplayStyle.None;
                if (_icon != null) {
                    _icon.style.backgroundImage = new StyleBackground();
                }
                HideTooltip();
                return;
            }

            _root.style.display = DisplayStyle.Flex;

            if (_icon != null) {
                _icon.style.backgroundImage = globalEvent.Icon == null
                    ? new StyleBackground()
                    : new StyleBackground(globalEvent.Icon);
            }

            if (_tooltipTitle != null) {
                _tooltipTitle.text = globalEvent.EventName;
            }

            if (_tooltipDescription != null) {
                _tooltipDescription.text = globalEvent.Description;
            }

            HideTooltip();
        }

        private void OnPointerEnter(PointerEnterEvent evt) {
            if (_root.resolvedStyle.display == DisplayStyle.None) return;
            if (_tooltip != null) {
                _tooltip.style.display = DisplayStyle.Flex;
            }
        }

        private void OnPointerLeave(PointerLeaveEvent evt) {
            HideTooltip();
        }

        private void HideTooltip() {
            if (_tooltip != null) {
                _tooltip.style.display = DisplayStyle.None;
            }
        }
    }
}
