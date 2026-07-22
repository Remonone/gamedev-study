using System;
using System.Collections.Generic;
using Data.Input;
using Data.Results;
using R3;
using R3.Triggers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Presentation {
    public sealed class DocumentCollector : MonoBehaviour {
        private enum CollectionState {
            Available,
            Collecting,
            Collected
        }

        [SerializeField] private LayerMask _acceptedSourceLayers;

        private readonly Dictionary<DocumentView, CollectionState> _documentStates = new();
        private readonly Subject<SignatureAttempt> _collected = new();
        private Graphic _dropSurface;
        private IDisposable _dropSubscription;

        public Observable<SignatureAttempt> Collected => _collected;

        private void Awake() {
            _dropSurface = GetComponent<Graphic>();
        }

        private void OnEnable() {
            if (_dropSurface == null) {
                Debug.LogError("DocumentCollector requires a uGUI Graphic drop surface.", this);
                return;
            }

            _dropSubscription = _dropSurface.OnDropAsObservable().Subscribe(OnDrop);
        }

        private void OnDisable() {
            _dropSubscription?.Dispose();
            _dropSubscription = null;
        }

        private void OnDestroy() {
            _dropSubscription?.Dispose();
            _dropSubscription = null;
            _collected.Dispose();
        }

        public bool TryCollect(DocumentView document, float endTime, out SignatureAttempt attempt) {
            attempt = null;
            PruneDestroyedDocuments();

            if (!isActiveAndEnabled || document == null || !document.isActiveAndEnabled ||
                document.ViewModel == null || float.IsNaN(endTime) || float.IsInfinity(endTime) ||
                _documentStates.TryGetValue(document, out var state) && state != CollectionState.Available) {
                return false;
            }

            _documentStates.Add(document, CollectionState.Collecting);
            try {
                attempt = document.CollectSignature(endTime);
                _documentStates[document] = CollectionState.Collected;
                return true;
            }
            catch {
                _documentStates.Remove(document);
                throw;
            }
        }

        private void OnDrop(PointerEventData eventData) {
            GameObject pointerDrag = eventData.pointerDrag;
            if (pointerDrag == null ||
                (_acceptedSourceLayers.value & (1 << pointerDrag.layer)) == 0) return;

            DocumentView document = pointerDrag.GetComponentInParent<DocumentView>();
            if (document == null) return;

            bool destructionScheduled = false;
            try {
                if (!TryCollect(document, Time.unscaledTime, out SignatureAttempt attempt)) return;
                try {
                    _collected.OnNext(attempt);
                    document.ViewModel.Evaluate(attempt);
                }
                finally {
                    if (document != null) {
                        destructionScheduled = true;
                        Destroy(document.gameObject);
                    }
                }
            }
            catch (Exception exception) {
                Debug.LogException(exception, this);
                if (!destructionScheduled && document != null) Destroy(document.gameObject);
            }
        }

        private void PruneDestroyedDocuments() {
            if (_documentStates.Count == 0) return;

            List<DocumentView> destroyedDocuments = null;
            foreach (DocumentView document in _documentStates.Keys) {
                if (document != null) continue;

                destroyedDocuments ??= new List<DocumentView>();
                destroyedDocuments.Add(document);
            }

            if (destroyedDocuments == null) return;
            foreach (DocumentView document in destroyedDocuments) {
                _documentStates.Remove(document);
            }
        }
    }
}
