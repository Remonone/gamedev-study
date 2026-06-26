using System;
using System.Collections.Generic;
using R3;
using UnityEngine.UIElements;
using Views.Models;

namespace Views {
    public class NotificationCenterView : IDisposable {
        private readonly VisualElement _container;
        private readonly VisualTreeAsset _itemTemplate;

        private readonly Dictionary<NotificationItemViewModel, NotificationItemView> _views = new();

        private CompositeDisposable _disposable = new();
        private NotificationCenterViewModel _viewModel;

        public NotificationCenterView(VisualElement container, VisualTreeAsset itemTemplate) {
            _container = container;
            _itemTemplate = itemTemplate;
        }

        public void Bind(NotificationCenterViewModel viewModel) {
            _disposable.Dispose();
            _disposable = new CompositeDisposable();

            _viewModel = viewModel;
            if (_viewModel == null) {
                return;
            }

            _viewModel.ActiveNotifications.Subscribe(Render).AddTo(_disposable);
        }

        private void Render(IReadOnlyList<NotificationItemViewModel> active) {
            var activeSet = new HashSet<NotificationItemViewModel>(active);

            var toRemove = new List<NotificationItemViewModel>();
            foreach (var pair in _views) {
                if (!activeSet.Contains(pair.Key)) {
                    pair.Value.Root.RemoveFromHierarchy();
                    pair.Value.Dispose();
                    toRemove.Add(pair.Key);
                }
            }
            foreach (var vm in toRemove) {
                _views.Remove(vm);
            }

            foreach (var vm in active) {
                if (_views.ContainsKey(vm)) {
                    continue;
                }

                var root = CreateItemRoot();
                var view = new NotificationItemView(root);
                view.Bind(vm);
                _container.Add(root);
                _views[vm] = view;
            }
        }

        private VisualElement CreateItemRoot() {
            if (_itemTemplate != null) {
                var container = _itemTemplate.Instantiate();
                var instantiationRoot = container.childCount > 0 ? container[0] : container;
                return instantiationRoot;
            }

            var root = new VisualElement();
            root.AddToClassList("notification");

            var icon = new VisualElement { name = "Icon" };
            icon.AddToClassList("notification__icon");

            var text = new VisualElement();
            text.AddToClassList("notification__text");

            var title = new Label { name = "Title" };
            title.AddToClassList("notification__title");

            var message = new Label { name = "Message" };
            message.AddToClassList("notification__message");

            text.Add(title);
            text.Add(message);
            root.Add(icon);
            root.Add(text);

            return root;
        }

        public void Dispose() {
            foreach (var view in _views.Values) {
                view.Dispose();
            }
            _views.Clear();
            _disposable.Dispose();
        }
    }
}