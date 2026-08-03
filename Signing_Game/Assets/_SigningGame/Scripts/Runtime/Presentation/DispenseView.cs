using System;
using Cysharp.Threading.Tasks;
using Data.Documents;
using Data.Cache;
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
        private IReadOnlyCacheData<DocumentEntries> _documents;
        private DocumentDispenser _dispenser;
        private DocumentGeneratorService _generator;
        private IDisposable _dragObservation;
        private IDisposable _documentHoverObservation;
        private bool _isPointerOverDispenseArea;
        private bool _isPointerOverDocument;
        private bool _isRetreatScheduled;
        
        private StableRandom _random;
        
        public void OnPointerEnter(PointerEventData eventData) {
            _isPointerOverDispenseArea = true;
            PresentDocument();
        }

        private void PresentDocument() {
            if (_activeDocument == null && !TrySummonDocument()) return;

            MoveActiveDocument(_shallowDocumentActive.position);
        }

        private bool TrySummonDocument() {
            if (!_generator.TryObtainDocument()) return false;

            _activeDocument = _dispenser.Spawn(BuildContext());
            var dragView = _activeDocument.GetComponent<DocumentDragView>();
            _dragObservation = dragView.IsDragging.Where(dragging => dragging).Subscribe(OnDrag);
            _documentHoverObservation = dragView.IsPointerOver.Subscribe(OnDocumentHoverChanged);
            _activeDocument.transform.position = _shallowDocumentRest.position;
            return true;
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

        private IDocumentContext BuildContext() {
            DocumentProperties properties = new DocumentProperties();
            properties.AddBehavior(_random.NextUInt64());
            var initialHue = 1f - 0.225f;
            var hue = initialHue - _documents.Value.SelectedDocumentQualityLevel * 0.025f;
            var color = Color.HSVToRGB(hue, 0.8f, 0.8f);
            properties.AddBehavior(color);
            return properties;
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
            _activeDocument = null;
        }

        public UniTask InitializeAsync(IServiceScope scope) {
            var stash = scope.Get<PlayerStatStash>();
            _dispenser = scope.Get<DocumentDispenser>();
            _generator = scope.Get<DocumentGeneratorService>();
            _documents = stash.Documents;
            _random = new StableRandom((ulong)Time.deltaTime);
            return UniTask.CompletedTask;
        }
    }
}
