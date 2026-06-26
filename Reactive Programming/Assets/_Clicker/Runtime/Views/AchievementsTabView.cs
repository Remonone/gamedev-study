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
        private readonly List<AchievementItemView> _itemViews = new();
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
            DisposeItemViews();
            _disposable.Dispose();
        }

        private void RenderAchievements(IReadOnlyList<AchievementItemViewModel> achievements) {
            DisposeItemViews();
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
                _itemViews.Add(itemView);
            }
        }

        private void DisposeItemViews() {
            foreach (var view in _itemViews) {
                view.Dispose();
            }
            _itemViews.Clear();
        }

        private VisualElement CreateAchievementRoot() {
            if (_itemTemplate != null) {
                return _itemTemplate.CloneTree();
            }

            var root = new VisualElement();
            root.AddToClassList("achievement-item");

            var header = new VisualElement();
            header.AddToClassList("achievement-item__header");

            var icon = new VisualElement { name = "Icon" };
            icon.AddToClassList("achievement-item__icon");

            var textWrap = new VisualElement();
            textWrap.AddToClassList("achievement-item__text");

            var name = new Label { name = "Name" };
            name.AddToClassList("achievement-item__name");

            var description = new Label { name = "Description" };
            description.AddToClassList("achievement-item__description");

            var reward = new Label { name = "Reward" };
            reward.AddToClassList("achievement-item__reward");

            var progress = new ProgressBar { name = "Progress" };
            progress.AddToClassList("achievement-item__progress");

            textWrap.Add(name);
            textWrap.Add(description);
            header.Add(icon);
            header.Add(textWrap);
            root.Add(header);
            root.Add(progress);
            root.Add(reward);

            return root;
        }

        private void OnAchievementClicked(string achievementId) {
            _viewModel?.OnAchievementClicked(achievementId);
        }
    }
}
