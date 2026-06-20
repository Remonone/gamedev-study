using UnityEngine;

namespace Types.Modifiers.Definitions {
    public readonly struct NotificationRequest {
        public readonly string Title;
        public readonly string Message;
        public readonly Sprite Icon;
        public readonly float Duration;
        public readonly NotificationType Type;
        public readonly NotificationPriority Priority;

        public NotificationRequest(
            string title,
            string message,
            NotificationType type,
            NotificationPriority priority = NotificationPriority.Medium,
            float duration = 5f,
            Sprite icon = null) {
            Title = title;
            Message = message;
            Type = type;
            Priority = priority;
            Duration = duration;
            Icon = icon;
        }
    }

    public enum NotificationType {
        Achievement,
        Reward,
        Info,
        Tutorial,
        System
    }
    
    public enum NotificationPriority {
        Low,
        Medium,
        High,
        Critical
    }
}