using R3;
using Types.Enums;
using UnityEngine;

namespace Views.Models {
    public class NotificationItemViewModel {
        public string Title { get; }
        public string Message { get; }
        public NotificationType Type { get; }
        public Sprite Icon { get; }

        private readonly ReactiveProperty<NotificationVisualState> _state;
        
        public ReadOnlyReactiveProperty<NotificationVisualState> State => _state;
        
        public NotificationItemViewModel(NotificationRequest request) {
            _state = new ReactiveProperty<NotificationVisualState>(NotificationVisualState.Opening);
            Title = request.Title;
            Message = request.Message;
            Type = request.Type;
            Icon = request.Icon;
        }

        public void SetVisible() {
            _state.Value = NotificationVisualState.Visible;
        }
        
        public void SetClosing() {
            _state.Value = NotificationVisualState.Closing;
        }
    }

    public enum NotificationVisualState {
        Opening,
        Visible,
        Closing
    }
}