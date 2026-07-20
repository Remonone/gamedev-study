using System;
using Contracts;
using Data.Input;
using Data.Results;
using Data.Rules;
using Authoring;
using R3;
using Services;

namespace Presentation {
    public sealed class DocumentViewModel : IDisposable {
        private readonly ISignatureRecorder _signatureRecorder;
        private readonly ISignatureEvaluator _evaluator;
        private readonly SignaturePresetDefinition _preset;
        private readonly SignatureDifficultyRules _difficulty;
        private readonly SignatureRuleModifiers _modifiers;
        private readonly Subject<SignatureEvaluationResult> _evaluated = new();
        private bool _disposed;

        public bool IsSigning => _signatureRecorder.IsAttemptActive;
        public bool IsStrokeActive => _signatureRecorder.IsStrokeActive;
        public bool CanCompleteSignature => IsSigning && !IsStrokeActive;
        public Observable<SignatureEvaluationResult> Evaluated => _evaluated;

        public DocumentViewModel()
            : this(new SignatureRecorder()) {
        }

        public DocumentViewModel(ISignatureRecorder signatureRecorder) {
            _signatureRecorder = signatureRecorder
                ?? throw new ArgumentNullException(nameof(signatureRecorder));
        }

        public DocumentViewModel(ISignatureRecorder signatureRecorder, ISignatureEvaluator evaluator,
            SignaturePresetDefinition preset, SignatureDifficultyRules difficulty, SignatureRuleModifiers modifiers)
            : this(signatureRecorder) {
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            _preset = preset ?? throw new ArgumentNullException(nameof(preset));
            _difficulty = difficulty ?? throw new ArgumentNullException(nameof(difficulty));
            _modifiers = modifiers;
        }

        public DocumentViewModel(ISignatureEvaluator evaluator, SignaturePresetDefinition preset,
            SignatureDifficultyRules difficulty, SignatureRuleModifiers modifiers)
            : this(new SignatureRecorder(), evaluator, preset, difficulty, modifiers) {
        }

        public SignatureEvaluationResult Evaluate(SignatureAttempt attempt) {
            if (_disposed) throw new ObjectDisposedException(nameof(DocumentViewModel));
            SignatureEvaluationResult result = _evaluator.Evaluate(attempt, _preset, _difficulty, _modifiers);
            _evaluated.OnNext(result);
            return result;
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

        public void Dispose() {
            if (_disposed) return;
            _disposed = true;
            _evaluated.Dispose();
        }
    }
}
