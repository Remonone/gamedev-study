using System;
using System.Collections.Generic;
using R3;
using Services;
using Types.Enums;

namespace Views.Models {
    public class NotificationCenterViewModel : IDisposable {
        private readonly NotificationService _notificationService;
        private readonly Queue<NotificationRequest> _pending = new();
        private readonly List<NotificationItemViewModel> _items = new();
        
        private readonly ReactiveProperty<IReadOnlyList<NotificationItemViewModel>> _activeNotifications = new();
        private DisposableBag _disposables;

        private readonly int _maxVisible;
        private readonly float _closeAnimationSeconds;
        
        public ReadOnlyReactiveProperty<IReadOnlyList<NotificationItemViewModel>> ActiveNotifications => _activeNotifications;

        public NotificationCenterViewModel(NotificationService notificationService, int maxVisible = 3,
            float closeAnimationSeconds = 0.25f) {
            if (maxVisible <= 0)
                throw new ArgumentException("Max visible must be greater than 0");

            _notificationService = notificationService;
            _maxVisible = maxVisible;
            _closeAnimationSeconds = closeAnimationSeconds;
            
            _activeNotifications = new ReactiveProperty<IReadOnlyList<NotificationItemViewModel>>(Array.Empty<NotificationItemViewModel>());
            
            _notificationService.OnNotification
                .Subscribe(OnNotification)
                .AddTo(ref _disposables);
        }

        private void OnNotification(NotificationRequest notification) {
            _pending.Enqueue(notification);
            TryShowNext();
        }

        private void TryShowNext() {
            while (_items.Count < _maxVisible && _pending.Count > 0) {
                ShowNext();
            }
        }

        private void ShowNext() {
            NotificationRequest notification = _pending.Dequeue();
            
            var itemViewModel = new NotificationItemViewModel(notification);
            
            _items.Add(itemViewModel);
            PublishActiveNotifications();
            
            itemViewModel.SetVisible();
            
            Observable.Timer(TimeSpan.FromSeconds(notification.Duration))
                .Subscribe(_ => BeginClose(itemViewModel))
                .AddTo(ref _disposables);
        }

        private void BeginClose(NotificationItemViewModel model) {
            if (!_items.Contains(model))
                return;
            model.SetClosing();
            
            Observable.Timer(TimeSpan.FromSeconds(_closeAnimationSeconds))
                .Subscribe(_ => Remove(model))
                .AddTo(ref _disposables);
        }

        private void Remove(NotificationItemViewModel model) {
            if (!_items.Contains(model))
                return;
            
            _items.Remove(model);
            PublishActiveNotifications();
        }

        private void PublishActiveNotifications() {
            _activeNotifications.Value = _items.ToArray();
        }
        
        
        public void Dispose() {
            _disposables.Dispose();
            
            _pending.Clear();
            _items.Clear();
            
            PublishActiveNotifications();
            
            _activeNotifications.Dispose();
        }
    }
}