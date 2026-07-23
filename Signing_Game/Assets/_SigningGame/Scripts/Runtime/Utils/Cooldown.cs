using System;
using R3;
using UnityEngine;

namespace Utils {
    public sealed class Cooldown : IDisposable {
        private const float CompletionThreshold = 0.0001f;

        private readonly Observable<float> _deltaTimeSource;

        private readonly ReactiveProperty<float> _duration;
        private readonly ReactiveProperty<float> _remaining;
        private readonly ReactiveProperty<float> _remainingRatio;
        private readonly ReactiveProperty<bool> _isRunning;

        private readonly Subject<Unit> _started = new();
        private readonly Subject<Unit> _completed = new();
        private readonly Subject<Unit> _cancelled = new();

        private IDisposable _tickSubscription;
        private bool _isDisposed;

        public Cooldown(float durationSeconds, Observable<float> deltaTimeSource) {
            _deltaTimeSource = deltaTimeSource
                ?? throw new ArgumentNullException(nameof(deltaTimeSource));
            float duration = ValidateDuration(durationSeconds);

            _duration = new ReactiveProperty<float>(duration);
            _remaining = new ReactiveProperty<float>(0f);
            _remainingRatio = new ReactiveProperty<float>(0f);
            _isRunning = new ReactiveProperty<bool>(false);
        }

        public ReadOnlyReactiveProperty<float> Duration => _duration;

        public ReadOnlyReactiveProperty<float> Remaining => _remaining;

        public ReadOnlyReactiveProperty<float> RemainingRatio => _remainingRatio;

        public ReadOnlyReactiveProperty<bool> IsRunning => _isRunning;

        public Observable<Unit> Started => _started;

        public Observable<Unit> Completed => _completed;

        public Observable<Unit> Cancelled => _cancelled;

        public bool IsReady => !_isRunning.CurrentValue;

        public bool TryStart() {
            if(_isDisposed) throw new ObjectDisposedException(nameof(Cooldown));

            if (_isRunning.CurrentValue) {
                return false;
            }

            StartInternal();
            return true;
        }
        
        public void Restart() {
            if(_isDisposed) throw new ObjectDisposedException(nameof(Cooldown));
            StopTicking();
            StartInternal();
        }

        
        public void Cancel() {
            if(_isDisposed) throw new ObjectDisposedException(nameof(Cooldown));

            if (!_isRunning.CurrentValue) return;

            StopTicking();

            _remaining.Value = 0f;
            _remainingRatio.Value = 0f;
            _isRunning.Value = false;

            _cancelled.OnNext(Unit.Default);
        }

        public void SetDuration(float durationSeconds) {
            if(_isDisposed) throw new ObjectDisposedException(nameof(Cooldown));

            float newDuration = ValidateDuration(durationSeconds);
            float oldDuration = _duration.CurrentValue;

            if (Mathf.Approximately(oldDuration, newDuration)) return;

            if (!_isRunning.CurrentValue) {
                _duration.Value = newDuration;
                return;
            }

            float oldRemaining = _remaining.CurrentValue;

            float newRemaining = Mathf.Max(0f, newDuration - Mathf.Max(0f, oldDuration - oldRemaining));

            _duration.Value = newDuration;
            _remaining.Value = newRemaining;

            UpdateRemainingRatio();

            if (newRemaining <= CompletionThreshold) {
                Complete();
            }
        }

        public void ReduceRemaining(float seconds) {
            if(_isDisposed) throw new ObjectDisposedException(nameof(Cooldown));

            if (seconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Cooldown reduction cannot be negative.");
            
            if (!_isRunning.CurrentValue || seconds <= 0f) return;
            
            _remaining.Value = Mathf.Max(0f, _remaining.CurrentValue - seconds);

            UpdateRemainingRatio();

            if (_remaining.CurrentValue <= CompletionThreshold) Complete();
        }

        
        public void AddRemaining(float seconds) {
            if(_isDisposed) throw new ObjectDisposedException(nameof(Cooldown));

            if (seconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Added cooldown time cannot be negative.");

            if (!_isRunning.CurrentValue || seconds <= 0f) return;

            _remaining.Value = Mathf.Min(
                _duration.CurrentValue,
                _remaining.CurrentValue + seconds);

            UpdateRemainingRatio();
        }

        private void StartInternal() {
            float duration = _duration.CurrentValue;

            if (duration <= CompletionThreshold) {
                _remaining.Value = 0f;
                _remainingRatio.Value = 0f;
                _isRunning.Value = false;

                _started.OnNext(Unit.Default);
                _completed.OnNext(Unit.Default);
                return;
            }

            _remaining.Value = duration;
            _remainingRatio.Value = 1f;
            _isRunning.Value = true;

            _started.OnNext(Unit.Default);

            _tickSubscription = _deltaTimeSource.Subscribe(Tick);
        }

        private void Tick(float deltaTime) {
            if (!_isRunning.CurrentValue || deltaTime <= 0f)
                return;

            _remaining.Value = Mathf.Max(
                0f,
                _remaining.CurrentValue - deltaTime);

            UpdateRemainingRatio();

            if (_remaining.CurrentValue <= CompletionThreshold)
                Complete();
        }

        private void Complete() {
            if (!_isRunning.CurrentValue)
                return;

            StopTicking();

            _remaining.Value = 0f;
            _remainingRatio.Value = 0f;
            _isRunning.Value = false;

            _completed.OnNext(Unit.Default);
        }

        private void UpdateRemainingRatio() {
            float duration = _duration.CurrentValue;

            _remainingRatio.Value = duration <= CompletionThreshold ? 0f : Mathf.Clamp01(_remaining.CurrentValue / duration);
        }

        private void StopTicking() {
            _tickSubscription?.Dispose();
            _tickSubscription = null;
        }

        private static float ValidateDuration(float duration) {
            if (float.IsNaN(duration) || float.IsInfinity(duration))
                throw new ArgumentOutOfRangeException(nameof(duration), duration, "Cooldown duration must be a finite number.");
            
            return Mathf.Max(0f, duration);
        }

        public void Dispose() {
            if (_isDisposed)
                return;

            _isDisposed = true;

            StopTicking();

            _started.Dispose();
            _completed.Dispose();
            _cancelled.Dispose();

            _duration.Dispose();
            _remaining.Dispose();
            _remainingRatio.Dispose();
            _isRunning.Dispose();
        }
    }
}