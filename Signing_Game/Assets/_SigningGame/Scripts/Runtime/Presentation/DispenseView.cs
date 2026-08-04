using System;
using System.Collections.Generic;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Documents;
using DG.Tweening;
using R3;
using Services;
using Services.Locator;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;
using Utils.Text.Generator;

namespace Presentation {
    public class DispenseView : MonoBehaviour,
        IService,
        IInitialize,
        IPointerEnterHandler, 
        IPointerExitHandler {
        [SerializeField] private Transform _shallowDocumentRest;
        [SerializeField] private Transform _shallowDocumentActive;

        private DocumentView _activeDocument;
        private DocumentDispenser _dispenser;
        private DispenseViewModel _viewModel;
        private IDisposable _dragObservation;
        private IDisposable _documentHoverObservation;
        private bool _isPointerOverDispenseArea;
        private bool _isPointerOverDocument;
        private bool _isRetreatScheduled;
        
        public void OnPointerEnter(PointerEventData eventData) {
            _isPointerOverDispenseArea = true;
            PresentDocument();
        }

        private void PresentDocument() {
            if (_activeDocument == null && !TrySummonDocument()) return;

            MoveActiveDocument(_shallowDocumentActive.position);
        }

        private bool TrySummonDocument() {
            if (!_viewModel.TryCreateContext(out IDocumentContext context)) return false;

            try {
                _activeDocument = _dispenser.Spawn(context);
                var dragView = _activeDocument.GetComponent<DocumentDragView>();
                if (dragView == null) {
                    throw new InvalidOperationException("A dispensed document requires DocumentDragView.");
                }

                _activeDocument.transform.position = _shallowDocumentRest.position;
                _dragObservation = dragView.IsDragging.Where(dragging => dragging).Subscribe(OnDrag);
                _documentHoverObservation = dragView.IsPointerOver.Subscribe(OnDocumentHoverChanged);
                return true;
            }
            catch (Exception exception) {
                ClearDocumentSubscriptions();
                context.Dispose();
                if (_activeDocument != null) {
                    _activeDocument.ViewModel?.Dispose();
                    Destroy(_activeDocument.gameObject);
                    _activeDocument = null;
                }

                Debug.LogException(exception, this);
                return false;
            }
        }
        
        private void OnDrag(bool dragging) {
            ReleaseActiveDocument();
        }

        private void OnDocumentHoverChanged(bool isPointerOverDocument) {
            _isPointerOverDocument = isPointerOverDocument;

            if (isPointerOverDocument) PresentDocument();
            else ScheduleRetreatIfPointerLeft();
        }

        private void ReleaseActiveDocument() {
            ClearDocumentSubscriptions();
            _isPointerOverDocument = false;
            _activeDocument = null;
        }

        public void OnPointerExit(PointerEventData eventData) {
            _isPointerOverDispenseArea = false;
            ScheduleRetreatIfPointerLeft();
        }

        private void ScheduleRetreatIfPointerLeft() {
            if (_activeDocument == null ||
                _isPointerOverDispenseArea ||
                _isPointerOverDocument ||
                _isRetreatScheduled) {
                return;
            }

            _isRetreatScheduled = true;
            RetreatIfPointerStillLeftAsync().Forget();
        }

        private async UniTaskVoid RetreatIfPointerStillLeftAsync() {
            try {
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, this.GetCancellationTokenOnDestroy());
            } catch (OperationCanceledException) {
                return;
            }

            _isRetreatScheduled = false;

            if (_activeDocument == null || _isPointerOverDispenseArea || _isPointerOverDocument) return;

            MoveActiveDocument(_shallowDocumentRest.position);
        }

        private void MoveActiveDocument(Vector3 position) {
            if (_activeDocument == null) return;

            Transform documentTransform = _activeDocument.transform;
            documentTransform.DOKill();
            documentTransform
                .DOMove(position, 0.12f)
                .SetEase(Ease.OutCubic);
        }

        private void ClearDocumentSubscriptions() {
            _dragObservation?.Dispose();
            _dragObservation = null;
            _documentHoverObservation?.Dispose();
            _documentHoverObservation = null;
        }
        
        public void Dispose() {
            ClearDocumentSubscriptions();
            if (_activeDocument != null) {
                _activeDocument.ViewModel?.Dispose();
                Destroy(_activeDocument.gameObject);
            }
            _activeDocument = null;
        }

        public UniTask InitializeAsync(IServiceScope scope) {
            _dispenser = scope.Get<DocumentDispenser>();
            var producers = new List<IDocumentProducer>();
            for (int index = 0; scope.TryGet(out IDocumentProducer producer, index); index++) {
                producers.Add(producer);
            }

            _viewModel = new DispenseViewModel(
                producers,
                scope.Get<PlayerStatStash>().Documents,
                new StableRandom((ulong)Time.deltaTime));
            return UniTask.CompletedTask;
        }
    }
}
