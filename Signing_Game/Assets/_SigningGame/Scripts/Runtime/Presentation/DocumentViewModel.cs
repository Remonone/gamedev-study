using System;
using Contracts;
using Data.Documents;
using Data.Input;
using Services;
using UnityEngine;

namespace Presentation {
    public sealed class DocumentViewModel : IDisposable {
        private ISignatureRecorder _signatureRecorder;
        private PlayerSignatureAcceptor _acceptor;
        private IDocumentSession _session;
        private bool _evaluated;
        private bool _disposed;
        
        private Color _headerColor = Color.HSVToRGB(1f - 0.225f, .8f, .8f);
        private ulong _textSeed = (ulong)UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        
        public Color HeaderColor => _headerColor;
        public ulong TextSeed => _textSeed;

        public bool IsSigning => _signatureRecorder.IsAttemptActive;
        public bool IsStrokeActive => _signatureRecorder.IsStrokeActive;
        public bool IsStamped { get; private set; }

        public void Evaluate(SignatureAttempt attempt) {
            if (_disposed) throw new ObjectDisposedException(nameof(DocumentViewModel));
            if (_evaluated) throw new InvalidOperationException("A document can only be evaluated once.");
            _evaluated = true;
            _acceptor.AcceptSignature(attempt, _session, IsStamped);
        }

        public void MarkStamped() {
            if (_disposed) throw new ObjectDisposedException(nameof(DocumentViewModel));
            IsStamped = true;
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

        private void SetContext(IDocumentContext context) {
            if (context == null) throw new ArgumentNullException(nameof(context));
            try {
                context.GetBehavior<ulong>(out _textSeed);
                context.GetBehavior<Color>(out _headerColor);
                _session = context.TakeSession();
            }
            finally {
                context.Dispose();
            }
        }

        private void SetAcceptor(PlayerSignatureAcceptor acceptor) {
            _acceptor = acceptor ?? throw new ArgumentNullException(nameof(acceptor));
        }

        private void SetRecorder(ISignatureRecorder recorder) {
            _signatureRecorder = recorder;
        }

        public void CancelSignature() {
            _signatureRecorder.CancelAttempt();
        }

        public void Dispose() {
            if (_disposed) return;
            _disposed = true;
            _session?.Dispose();
            _session = null;
        }
        
        public class DocumentViewModelBuilder {
            private DocumentViewModel _model;
        
            public DocumentViewModelBuilder() {
                _model = new DocumentViewModel();
            }

            public DocumentViewModelBuilder SetContext(IDocumentContext context) {
                _model.SetContext(context);
                return this;
            }

            public DocumentViewModelBuilder SetAcceptor(PlayerSignatureAcceptor acceptor) {
                _model.SetAcceptor(acceptor);
                return this;
            }
            
            public DocumentViewModel Build() {
                var model = _model;
                model.SetRecorder(new SignatureRecorder());
                _model = new DocumentViewModel();
                return model;
            }

            public void Reset() {
                _model.Dispose();
                _model = new DocumentViewModel();
            }
        }
    }

    
}
