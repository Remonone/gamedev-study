using System;
using System.Collections.Generic;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Documents;
using Data.Sound;
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
        IPostInitialize,
        IPointerEnterHandler,
        IPointerExitHandler {
        [SerializeField] private Transform _shallowDocumentRest;
        [SerializeField] private Transform _shallowDocumentActive;
        [SerializeField] private AudioCue _documentSlideOut;
        [SerializeField] private AudioCue _documentSlideIn;
        [SerializeField] private StampView _stampView;

        private DocumentView _activeDocument;
        private DocumentDispenser _dispenser;
        private DispenseViewModel _viewModel;
        private AudioService _audioService;
        private DispensedDocumentPresentation _displayedPresentation;
        private DispensedDocumentPresentation _deferredPresentation;
        private IDisposable _viewModelObservation;
        private IDisposable _qualityObservation;
        private IDisposable _dragObservation;
        private IDisposable _documentHoverObservation;
        private bool _isPointerOverDispenseArea;
        private bool _isPointerOverDocument;
        private bool _isRetreatScheduled;
        private bool _isAcquiring;
        private bool _shouldPlayHideSound;

        public void OnPointerEnter(PointerEventData eventData) {
            _isPointerOverDispenseArea = true;
            PresentDocument();
        }

        public void OnPointerExit(PointerEventData eventData) {
            _isPointerOverDispenseArea = false;
            ScheduleRetreatIfPointerLeft();
        }

        public UniTask PostInitializeAsync(IServiceScope scope) {
            _dispenser = scope.Get<DocumentDispenser>();
            _audioService = scope.Container.Get<AudioService>();
            var producers = new List<IDocumentProducer>();
            for (int index = 0; scope.TryGet(out IDocumentProducer producer, index); index++) {
                producers.Add(producer);
            }

            _viewModel = new DispenseViewModel(
                producers,
                scope.Get<PlayerStatStash>().Documents,
                new StableRandom((ulong)Time.deltaTime));
            _viewModelObservation = _viewModel.Changed.Subscribe(OnPresentationChanged);
            _qualityObservation = scope.Get<DocumentQualityService>().Changed
                .Subscribe(_ => _viewModel.RefreshPresentation());
            InitializeStamp(scope);
            ApplyPresentation(_viewModel.Current);
            return UniTask.CompletedTask;
        }

        public void Dispose() {
            _viewModelObservation?.Dispose();
            _viewModelObservation = null;
            _qualityObservation?.Dispose();
            _qualityObservation = null;
            _viewModel?.Dispose();
            _viewModel = null;
            _stampView?.Dispose();
            _stampView = null;
            DestroyOwnedDocument();
            _deferredPresentation = null;
        }

        private void InitializeStamp(IServiceScope scope) {
            if (_stampView == null) throw new InvalidOperationException("DispenseView requires a StampView.");
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) throw new InvalidOperationException("DispenseView requires a parent Canvas.");

            _stampView.Initialize(scope.Get<UnlockService>(), canvas);
        }

        private void OnPresentationChanged(DispensedDocumentPresentation presentation) {
            if (_isAcquiring) {
                _deferredPresentation = presentation;
                return;
            }

            ApplyPresentation(presentation);
        }

        private void ApplyPresentation(DispensedDocumentPresentation presentation) {
            if (presentation == null) {
                DestroyOwnedDocument();
                return;
            }

            bool needsNewShell = _activeDocument == null ||
                                 _displayedPresentation == null ||
                                 _displayedPresentation.Kind != presentation.Kind;
            if (needsNewShell) {
                DestroyOwnedDocument();
                SpawnOwnedDocument(presentation);
            }
            else {
                _activeDocument.ShowPreview(presentation);
                _displayedPresentation = presentation;
                ConfigureDrag(_activeDocument, presentation.IsAvailable);
            }

            if (!presentation.IsAvailable) {
                MoveActiveDocument(_shallowDocumentRest.position);
            }
            else if (_isPointerOverDispenseArea || _isPointerOverDocument) {
                PresentDocument();
            }
        }

        private void SpawnOwnedDocument(DispensedDocumentPresentation presentation) {
            try {
                _activeDocument = _dispenser.SpawnPreview(presentation);
                _shouldPlayHideSound = true;
                _displayedPresentation = presentation;
                _activeDocument.transform.position = _shallowDocumentRest.position;
                DocumentDragView dragView = GetDragView(_activeDocument);
                _dragObservation = dragView.IsDragging.Where(dragging => dragging).Subscribe(OnDragStarted);
                _documentHoverObservation = dragView.IsPointerOver.Subscribe(OnDocumentHoverChanged);
                ConfigureDrag(_activeDocument, presentation.IsAvailable);
            }
            catch (Exception exception) {
                DestroyOwnedDocument();
                Debug.LogException(exception, this);
            }
        }

        private void ConfigureDrag(DocumentView document, bool available) {
            DocumentDragView dragView = GetDragView(document);
            dragView.SetBeginDragGate(TryAcquireDisplayedDocument);
            dragView.SetDragEnabled(available);
        }

        private bool TryAcquireDisplayedDocument() {
            if (_isAcquiring || _activeDocument == null || _displayedPresentation == null ||
                !_displayedPresentation.IsAvailable) {
                return false;
            }

            DocumentView document = _activeDocument;
            DispensedDocumentPresentation presentation = _displayedPresentation;
            bool bound = false;
            _isAcquiring = true;
            try {
                if (!_viewModel.TryCreateContext(presentation, out IDocumentContext context)) {
                    _viewModel.RefreshCurrent();
                    return false;
                }

                _dispenser.Bind(document, context, presentation);
                document.transform.DOKill();
                bound = true;
                return true;
            }
            catch (Exception exception) {
                Debug.LogException(exception, this);
                DestroyOwnedDocument();
                _viewModel.RefreshCurrent();
                return false;
            }
            finally {
                _isAcquiring = false;
                if (!bound) ApplyAfterFailedAcquisition();
            }
        }

        private void ApplyAfterFailedAcquisition() {
            DispensedDocumentPresentation latest = _deferredPresentation ?? _viewModel.Current;
            _deferredPresentation = null;
            ApplyPresentation(latest);
            if (_displayedPresentation == null || !_displayedPresentation.IsAvailable) {
                MoveActiveDocument(_shallowDocumentRest.position);
            }
        }

        private void OnDragStarted(bool _) {
            if (_activeDocument == null || _displayedPresentation == null) return;

            DocumentView released = _activeDocument;
            DocumentOfferKey claimedKey = _displayedPresentation.Key;
            released.transform.DOKill();
            GetDragView(released).SetBeginDragGate(null);
            ClearDocumentSubscriptions();
            _activeDocument = null;
            _shouldPlayHideSound = false;
            _displayedPresentation = null;
            _isPointerOverDocument = false;

            _isAcquiring = true;
            try {
                _viewModel.AdvanceAfterClaim(claimedKey);
            }
            finally {
                _isAcquiring = false;
            }

            DispensedDocumentPresentation next = _deferredPresentation ?? _viewModel.Current;
            _deferredPresentation = null;
            ApplyPresentation(next);
        }

        private void PresentDocument() {
            if (_activeDocument == null || _displayedPresentation == null ||
                !_displayedPresentation.IsAvailable) {
                return;
            }
            _audioService.PlayUI(_documentSlideOut);
            MoveActiveDocument(_shallowDocumentActive.position);
        }

        private void OnDocumentHoverChanged(bool isPointerOverDocument) {
            _isPointerOverDocument = isPointerOverDocument;
            if (isPointerOverDocument) PresentDocument();
            else ScheduleRetreatIfPointerLeft();
        }

        private void ScheduleRetreatIfPointerLeft() {
            if (_activeDocument == null || _isPointerOverDispenseArea || _isPointerOverDocument ||
                _isRetreatScheduled) {
                return;
            }
            if(_shouldPlayHideSound) _audioService.PlayUI(_documentSlideIn);
            _isRetreatScheduled = true;
            RetreatIfPointerStillLeftAsync().Forget();
        }

        private async UniTaskVoid RetreatIfPointerStillLeftAsync() {
            try {
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, this.GetCancellationTokenOnDestroy());
            }
            catch (OperationCanceledException) {
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
            documentTransform.DOMove(position, 0.12f).SetEase(Ease.OutCubic);
        }

        private void DestroyOwnedDocument() {
            ClearDocumentSubscriptions();
            _isPointerOverDocument = false;
            if (_activeDocument != null) {
                DocumentDragView dragView = _activeDocument.GetComponent<DocumentDragView>();
                if (dragView != null) dragView.SetBeginDragGate(null);
                _activeDocument.transform.DOKill();
                _activeDocument.ViewModel?.Dispose();
                Destroy(_activeDocument.gameObject);
            }

            _activeDocument = null;
            _displayedPresentation = null;
        }

        private void ClearDocumentSubscriptions() {
            _dragObservation?.Dispose();
            _dragObservation = null;
            _documentHoverObservation?.Dispose();
            _documentHoverObservation = null;
        }

        private static DocumentDragView GetDragView(DocumentView document) {
            DocumentDragView dragView = document.GetComponent<DocumentDragView>();
            if (dragView == null) throw new InvalidOperationException("A dispensed document requires DocumentDragView.");
            return dragView;
        }
    }
}
