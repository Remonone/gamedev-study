using System;
using R3;
using Types.Modifiers.Definitions;

namespace Services {
    public class NotificationService : IService, IDisposable {
        
        private Subject<NotificationRequest> _onNotification = new();
        public Observable<NotificationRequest> OnNotification => _onNotification;

        public NotificationService() { }

        public void Push(NotificationRequest request) {
            ValidateNotification(request); 
            _onNotification.OnNext(request);
        }
        
        private void ValidateNotification(NotificationRequest request) {
            if (string.IsNullOrEmpty(request.Title)) {
                throw new ArgumentException("Title cannot be null or empty");
            }
        }

        public void Dispose() {
            _onNotification.Dispose();
        }
    }
}