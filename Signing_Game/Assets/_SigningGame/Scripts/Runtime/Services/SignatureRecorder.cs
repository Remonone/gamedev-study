using System;
using System.Collections.Generic;
using Contracts;
using Data.Input;

namespace Services {
    public sealed class SignatureRecorder : ISignatureRecorder {
        private readonly List<SignatureStrokeAttempt> _strokes = new();

        private List<SignatureInputPoint> _currentPoints;
        private float _attemptStartTime;
        private float _lastPointTime;

        public bool IsAttemptActive { get; private set; }
        public bool IsStrokeActive => _currentPoints != null;

        public void BeginAttempt(float startTime) {
            if (IsAttemptActive) {
                throw new InvalidOperationException("A signature attempt is already active.");
            }

            ValidateTime(startTime, nameof(startTime));

            _strokes.Clear();
            _attemptStartTime = startTime;
            _lastPointTime = startTime;
            IsAttemptActive = true;
        }

        public void BeginStroke(SignatureInputPoint firstPoint) {
            EnsureAttemptActive();

            if (IsStrokeActive) {
                throw new InvalidOperationException("A signature stroke is already active.");
            }

            _currentPoints = new List<SignatureInputPoint> {
                EnsureNextPoint(firstPoint, nameof(firstPoint))
            };
        }

        public void AddPoint(SignatureInputPoint point) {
            EnsureStrokeActive();
            _currentPoints.Add(EnsureNextPoint(point, nameof(point)));
        }

        public void EndStroke(SignatureInputPoint finalPoint) {
            EnsureStrokeActive();
            _currentPoints.Add(EnsureNextPoint(finalPoint, nameof(finalPoint)));

            _strokes.Add(new SignatureStrokeAttempt(_currentPoints.ToArray()));
            _currentPoints = null;
        }

        public SignatureAttempt CompleteAttempt(float endTime) {
            EnsureAttemptActive();

            if (IsStrokeActive) {
                throw new InvalidOperationException("The active stroke must be ended before completing the attempt.");
            }

            ValidateTime(endTime, nameof(endTime));

            if (endTime < _lastPointTime) {
                throw new ArgumentOutOfRangeException(
                    nameof(endTime),
                    "End time cannot precede the last recorded point.");
            }

            var attempt = new SignatureAttempt(
                _strokes.ToArray(),
                endTime - _attemptStartTime);

            Reset();
            return attempt;
        }

        public void CancelAttempt() {
            Reset();
        }

        private SignatureInputPoint EnsureNextPoint(SignatureInputPoint point, string parameterName) {
            if (point == null) {
                throw new ArgumentNullException(parameterName);
            }

            ValidateTime(point.Time, parameterName);

            if (point.Time < _lastPointTime) {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Point time cannot precede the previously recorded time.");
            }

            _lastPointTime = point.Time;
            return point;
        }

        private static void ValidateTime(float time, string parameterName) {
            if (float.IsNaN(time) || float.IsInfinity(time)) {
                throw new ArgumentOutOfRangeException(parameterName, "Time must be finite.");
            }
        }

        private void EnsureAttemptActive() {
            if (!IsAttemptActive) {
                throw new InvalidOperationException("No signature attempt is active.");
            }
        }

        private void EnsureStrokeActive() {
            EnsureAttemptActive();

            if (!IsStrokeActive) {
                throw new InvalidOperationException("No signature stroke is active.");
            }
        }

        private void Reset() {
            _strokes.Clear();
            _currentPoints = null;
            _attemptStartTime = default;
            _lastPointTime = default;
            IsAttemptActive = false;
        }
    }
}
