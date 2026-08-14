using System;

namespace Services {
    public enum GameLaunchMode {
        Continue,
        NewGame
    }

    public sealed class GameSessionService : IService {
        private GameLaunchMode? _pendingMode;

        public bool HasPendingLaunch => _pendingMode.HasValue;

        public void Prepare(GameLaunchMode mode) {
            if (_pendingMode.HasValue) {
                throw new InvalidOperationException("A gameplay launch mode is already pending and has not been consumed.");
            }

            _pendingMode = mode;
        }

        public bool TryConsume(out GameLaunchMode mode) {
            if (!_pendingMode.HasValue) {
                mode = default;
                return false;
            }

            mode = _pendingMode.Value;
            _pendingMode = null;
            return true;
        }

        public void ClearPending() => _pendingMode = null;

        public void Dispose() => ClearPending();
    }
}
