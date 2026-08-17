using System;
using Data.Tutorial;
using R3;
using Services;
using UI;
using UnityEngine;

namespace Presentation.Tutorial {
    /// <summary>
    /// Bridge placed on the document prefab next to its signing field: publishes finished strokes
    /// as tutorial interactions. Inactive in scenes without a tutorial service.
    /// </summary>
    public sealed class TutorialSignatureStrokeBridge : MonoBehaviour {
        [SerializeField] private SigningField _field;

        private IDisposable _subscription;

        private void Start() {
            TutorialBridgeUtility.BindWhenReady(this, Subscribe);
        }

        private void OnDestroy() {
            _subscription?.Dispose();
            _subscription = null;
        }

        private void Subscribe(TutorialService tutorial) {
            if (_field == null) {
                Debug.LogWarning("TutorialSignatureStrokeBridge has no signing field reference.", this);
                return;
            }

            _subscription = _field.OnInput
                .Where(inputEvent => inputEvent.Type == SignatureInputEventType.StrokeEnded)
                .Subscribe(_ =>
                    tutorial.NotifyInteraction(new TutorialInteractionEvent(TutorialInteractionKind.SignatureStroke)));
        }
    }
}
