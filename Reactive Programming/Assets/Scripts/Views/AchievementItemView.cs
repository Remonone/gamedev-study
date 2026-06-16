using System;
using R3;
using UnityEngine.UIElements;
using Views.Models;

namespace Views {
    public class AchievementItemView {
        private readonly VisualElement _root;
        private readonly VisualElement _icon;
        private readonly Label _name;
        private readonly Label _description;
        private readonly Label _reward;
        private readonly ProgressBar _progressBar;

        private CompositeDisposable _disposable = new();
        private AchievementItemViewModel _data;
        private Action<string> _onItemClicked;
        private bool _isExpanded;

        public AchievementItemView(VisualElement root) {
            _root = root;
            _icon = _root.Q<VisualElement>("Icon");
            _name = _root.Q<Label>("Name");
            _description = _root.Q<Label>("Description");
            _reward = _root.Q<Label>("Reward");
            _progressBar = _root.Q<ProgressBar>("Progress");

            _root.RegisterCallback<ClickEvent>(OnClicked);
        }

        public void Bind(AchievementItemViewModel data, Action<string> onItemClicked) {
            _disposable.Dispose();
            _disposable = new CompositeDisposable();

            _data = data;
            _onItemClicked = onItemClicked;
            _isExpanded = false;

            _name.text = string.IsNullOrWhiteSpace(_data.Name) ? _data.Id : _data.Name;
            _description.text = _data.Description ?? string.Empty;

            if (_data.Icon != null) {
                _icon.style.backgroundImage = new StyleBackground(_data.Icon);
            } else {
                _icon.style.backgroundImage = new StyleBackground();
            }

            _root.RemoveFromClassList("achievement-item--expanded");

            _data.IsCompleted.Subscribe(OnCompletedChanged).AddTo(_disposable);

            if (_progressBar != null) {
                _data.Progress
                    .Subscribe(p => _progressBar.value = p * 100f)
                    .AddTo(_disposable);

                _data.ProgressText
                    .Subscribe(text => _progressBar.title = text)
                    .AddTo(_disposable);
            }
        }

        private void OnCompletedChanged(bool isCompleted) {
            if (isCompleted) {
                _root.RemoveFromClassList("achievement-item--locked");
                _reward.text = "Completed";
            } else {
                _root.AddToClassList("achievement-item--locked");
                _reward.text = string.Empty;
            }
        }

        private void OnClicked(ClickEvent _) {
            if (!_data.IsCompleted.CurrentValue) {
                return;
            }

            _isExpanded = !_isExpanded;
            if (_isExpanded) {
                _root.AddToClassList("achievement-item--expanded");
            } else {
                _root.RemoveFromClassList("achievement-item--expanded");
            }

            _onItemClicked?.Invoke(_data.Id);
        }

        public void Dispose() {
            _disposable.Dispose();
        }
    }
}
