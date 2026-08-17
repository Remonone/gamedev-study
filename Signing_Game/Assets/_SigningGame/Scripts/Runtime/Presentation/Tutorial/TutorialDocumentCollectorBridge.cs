using System;
using Data.Tutorial;
using DG.Tweening;
using R3;
using Services;
using UnityEngine;

namespace Presentation.Tutorial {
    /// <summary>
    /// Bridge on the document collector GameObject: publishes collected (transferred) documents
    /// as tutorial interactions and pulses the collector area while a slide awaits a transfer.
    /// </summary>
    public sealed class TutorialDocumentCollectorBridge : MonoBehaviour {
        [SerializeField] private DocumentCollector _collector;
        [SerializeField, Min(0.05f)] private float _pulseScale = 1.1f;
        [SerializeField, Min(0.1f)] private float _pulseDuration = 0.5f;

        private IDisposable _subscription;
        private Tween _pulse;
        private Vector3 _originalScale;

        private void Start() {
            TutorialBridgeUtility.BindWhenReady(this, Subscribe);
        }

        private void OnDisable() {
            StopPulse();
        }

        private void OnDestroy() {
            _subscription?.Dispose();
            _subscription = null;
            StopPulse();
        }

        public void Pulse() {
            if (_pulse != null) return;

            _originalScale = transform.localScale;
            _pulse = transform
                .DOScale(_originalScale * _pulseScale, _pulseDuration)
                .SetEase(Ease.InOutQuad)
                .SetLoops(-1, LoopType.Yoyo)
                .SetTarget(this);
        }

        public void StopPulse() {
            if (_pulse == null) return;

            _pulse.Kill();
            _pulse = null;
            transform.localScale = _originalScale;
        }

        private void Subscribe(TutorialService tutorial) {
            if (_collector == null) {
                Debug.LogWarning("TutorialDocumentCollectorBridge has no collector reference.", this);
                return;
            }

            _subscription = _collector.Collected.Subscribe(_ =>
                tutorial.NotifyInteraction(new TutorialInteractionEvent(TutorialInteractionKind.DocumentCollected)));
        }
    }
}
