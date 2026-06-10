using System;
using System.Collections.Generic;
using R3;
using UnityEngine.UIElements;
using Views.Models;

namespace Views {
    public class AchievementsTabView : IDisposable {
        private readonly VisualElement _root;
        private readonly ScrollView _list;
        private readonly VisualTreeAsset _itemTemplate;

        private CompositeDisposable _disposable = new();
        private AchievementsTabViewModel _viewModel;

        public AchievementsTabView(VisualElement root, VisualTreeAsset itemTemplate) {
            _root = root;
            _list = _root.Q<ScrollView>("AchievementsList");
            _itemTemplate = itemTemplate;
        }

        public void Bind(AchievementsTabViewModel viewModel) {
            _disposable.Dispose();
            _disposable = new CompositeDisposable();

            _viewModel = viewModel;
            if (_viewModel == null) {
                return;
            }

            _viewModel.Achievements.Subscribe(RenderAchievements).AddTo(_disposable);
            _viewModel.RequestInitialState();
        }

        public void Dispose() {
            _disposable.Dispose();
        }

        private void RenderAchievements(IReadOnlyList<AchievementViewData> achievements) {
            _list.Clear();

            if (achievements == null || achievements.Count == 0) {
                var empty = new Label("Achievements will appear here.");
                empty.AddToClassList("achievements-empty");
                _list.Add(empty);
                return;
            }

            foreach (var achievement in achievements) {
                var itemRoot = CreateAchievementRoot();
                var itemView = new AchievementItemView(itemRoot);
                itemView.Bind(achievement, OnAchievementClicked);
                _list.Add(itemRoot);
            }
        }

        private VisualElement CreateAchievementRoot() {
            if (_itemTemplate != null) {
                return _itemTemplate.CloneTree();
            }

            var root = new VisualElement();
            root.AddToClassList("achievement-item");

            var header = new VisualElement();
            header.AddToClassList("achievement-item__header");

            var icon = new VisualElement();
            icon.name = "Icon";
            icon.AddToClassList("achievement-item__icon");

            var textWrap = new VisualElement();
            textWrap.AddToClassList("achievement-item__text");

            var name = new Label {
                name = "Name"
            };
            name.AddToClassList("achievement-item__name");

            var description = new Label {
                name = "Description"
            };
            description.AddToClassList("achievement-item__description");

            var reward = new Label {
                name = "Reward"
            };
            reward.AddToClassList("achievement-item__reward");

            textWrap.Add(name);
            textWrap.Add(description);
            header.Add(icon);
            header.Add(textWrap);
            root.Add(header);
            root.Add(reward);

            return root;
        }

        private void OnAchievementClicked(string achievementId) {
            _viewModel?.OnAchievementClicked(achievementId);
        }
    }
}
