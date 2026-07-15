using System;
using Contracts;
using Data.Input;
using Services;

namespace Presentation {
    public sealed class DocumentViewModel {
        private readonly ISignatureRecorder _signatureRecorder;

        public bool IsSigning => _signatureRecorder.IsAttemptActive;
        public bool IsStrokeActive => _signatureRecorder.IsStrokeActive;
        public bool CanCompleteSignature => IsSigning && !IsStrokeActive;

        public DocumentViewModel()
            : this(new SignatureRecorder()) {
        }

        public DocumentViewModel(ISignatureRecorder signatureRecorder) {
            _signatureRecorder = signatureRecorder
                ?? throw new ArgumentNullException(nameof(signatureRecorder));
        }

        public void StartStroke(SignatureInputPoint firstPoint) {
            if (firstPoint == null) {
                throw new ArgumentNullException(nameof(firstPoint));
            }

            if (!IsSigning) {
                _signatureRecorder.BeginAttempt(firstPoint.Time);
            }

            _signatureRecorder.BeginStroke(firstPoint);
        }

        public void AddPoint(SignatureInputPoint point) {
            _signatureRecorder.AddPoint(point);
        }

        public void FinishStroke(SignatureInputPoint finalPoint) {
            _signatureRecorder.EndStroke(finalPoint);
        }

        public SignatureAttempt CompleteSignature(float endTime) {
            return _signatureRecorder.CompleteAttempt(endTime);
        }

        public SignatureAttempt CollectSignature(float endTime) {
            if (float.IsNaN(endTime) || float.IsInfinity(endTime)) {
                throw new ArgumentOutOfRangeException(nameof(endTime), "Time must be finite.");
            }

            if (!IsSigning) {
                return new SignatureAttempt(Array.Empty<SignatureStrokeAttempt>(), 0f);
            }

            if (IsStrokeActive) {
                throw new InvalidOperationException(
                    "The active stroke must be ended before collecting the signature.");
            }

            return _signatureRecorder.CompleteAttempt(endTime);
        }

        public void CancelSignature() {
            _signatureRecorder.CancelAttempt();
        }
    }
}
