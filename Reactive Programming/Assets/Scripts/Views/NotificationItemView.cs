using System;
using R3;
using Types;
using UnityEngine.UIElements;
using Views.Models;

namespace Views {
    public class NotificationItemView : IDisposable {
        public VisualElement Root { get; }

        private readonly VisualElement _icon;
        private readonly Label _title;
        private readonly Label _message;

        private CompositeDisposable _disposable = new();
        private NotificationItemViewModel _viewModel;

        public NotificationItemView(VisualElement root) {
            Root = root;
            _icon = Root.Q<VisualElement>("Icon");
            _title = Root.Q<Label>("Title");
            _message = Root.Q<Label>("Message");
        }

        public void Bind(NotificationItemViewModel viewModel) {
            _disposable.Dispose();
            _disposable = new CompositeDisposable();
            _viewModel = viewModel;

            _title.text = viewModel.Title ?? string.Empty;
            _message.text = viewModel.Message ?? string.Empty;

            if (_icon != null) {
                _icon.style.backgroundImage = viewModel.Icon != null
                    ? new StyleBackground(viewModel.Icon)
                    : new StyleBackground();
            }

            ApplyTypeClass(viewModel.Type);

            viewModel.State.Subscribe(OnStateChanged).AddTo(_disposable);
        }

        private void OnStateChanged(NotificationVisualState state) {
            Root.RemoveFromClassList("notification--opening");
            Root.RemoveFromClassList("notification--visible");
            Root.RemoveFromClassList("notification--closing");

            switch (state) {
                case NotificationVisualState.Opening:
                    Root.AddToClassList("notification--opening");
                    break;
                case NotificationVisualState.Visible:
                    Root.AddToClassList("notification--visible");
                    break;
                case NotificationVisualState.Closing:
                    Root.AddToClassList("notification--closing");
                    break;
            }
        }

        private void ApplyTypeClass(NotificationType type) {
            Root.RemoveFromClassList("notification--achievement");
            Root.RemoveFromClassList("notification--reward");
            Root.RemoveFromClassList("notification--info");
            Root.RemoveFromClassList("notification--tutorial");
            Root.RemoveFromClassList("notification--system");

            Root.AddToClassList("notification--" + type.ToString().ToLowerInvariant());
        }

        public void Dispose() {
            _disposable.Dispose();
        }
    }
}