using System;
using UnityEngine.UIElements;
using Views.Models;

namespace Views {
    public class AchievementItemView {
        private readonly VisualElement _root;
        private readonly VisualElement _icon;
        private readonly Label _name;
        private readonly Label _description;
        private readonly Label _reward;

        private AchievementViewData _data;
        private Action<string> _onItemClicked;
        private bool _isExpanded;

        public AchievementItemView(VisualElement root) {
            _root = root;
            _icon = _root.Q<VisualElement>("Icon");
            _name = _root.Q<Label>("Name");
            _description = _root.Q<Label>("Description");
            _reward = _root.Q<Label>("Reward");

            _root.RegisterCallback<ClickEvent>(OnClicked);
        }

        public void Bind(AchievementViewData data, Action<string> onItemClicked) {
            _data = data;
            _onItemClicked = onItemClicked;
            _isExpanded = false;

            _name.text = string.IsNullOrWhiteSpace(_data.Name) ? _data.Id : _data.Name;
            _description.text = _data.Description ?? string.Empty;
            _reward.text = $"Reward: {_data.Reward}";

            if (_data.Icon != null) {
                _icon.style.backgroundImage = new StyleBackground(_data.Icon);
            } else {
                _icon.style.backgroundImage = new StyleBackground();
            }

            _root.RemoveFromClassList("achievement-item--expanded");
            _root.RemoveFromClassList("achievement-item--locked");

            if (!_data.IsCompleted) {
                _root.AddToClassList("achievement-item--locked");
            }
        }

        private void OnClicked(ClickEvent _) {
            if (!_data.IsCompleted) {
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
    }
}
