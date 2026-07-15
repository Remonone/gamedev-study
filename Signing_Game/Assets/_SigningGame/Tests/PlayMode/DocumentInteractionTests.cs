using System;
using System.Collections;
using System.Reflection;
using Contracts;
using Data.Input;
using NUnit.Framework;
using Presentation;
using R3;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SigningGame.Tests.PlayMode {
    public sealed class DocumentInteractionTests {
        private GameObject _canvasObject;
        private GameObject _eventSystemObject;
        private EventSystem _eventSystem;

        [SetUp]
        public void SetUp() {
            _eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            _eventSystem = _eventSystemObject.GetComponent<EventSystem>();
            _canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            _canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        }

        [TearDown]
        public void TearDown() {
            UnityEngine.Object.DestroyImmediate(_canvasObject);
            UnityEngine.Object.DestroyImmediate(_eventSystemObject);
        }

        [UnityTest]
        public IEnumerator BackgroundLeftDrag_PreservesOffsetMovesAndRestoresCanvasGroup() {
            DocumentView document = CreateDocument(new Vector2(40f, 30f));
            var rect = (RectTransform)document.transform;
            var canvasGroup = document.GetComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = true;
            var drag = document.GetComponent<DocumentDragView>();
            yield return null;

            PointerEventData begin = Pointer(7, PointerEventData.InputButton.Left, new Vector2(50f, 35f));
            drag.OnBeginDrag(begin);
            Assert.That(canvasGroup.blocksRaycasts, Is.False);

            drag.OnDrag(Pointer(7, PointerEventData.InputButton.Left, new Vector2(80f, 55f)));
            Assert.That(rect.anchoredPosition.x, Is.EqualTo(70f).Within(0.01f));
            Assert.That(rect.anchoredPosition.y, Is.EqualTo(50f).Within(0.01f));

            drag.OnEndDrag(Pointer(7, PointerEventData.InputButton.Left, new Vector2(80f, 55f)));
            Assert.That(canvasGroup.blocksRaycasts, Is.True);
        }

        [UnityTest]
        public IEnumerator PadLeftInput_SignsWithoutMovingDocument() {
            DocumentView document = CreateDocument(new Vector2(20f, 10f));
            SigningField field = document.GetComponentInChildren<SigningField>();
            Vector2 originalPosition = ((RectTransform)document.transform).anchoredPosition;
            yield return null;

            field.OnPointerDown(Pointer(1, PointerEventData.InputButton.Left, Vector2.zero));
            field.OnDrag(Pointer(1, PointerEventData.InputButton.Left, new Vector2(10f, 5f)));
            field.OnPointerUp(Pointer(1, PointerEventData.InputButton.Left, new Vector2(12f, 6f)));

            Assert.That(document.ViewModel.CanCompleteSignature, Is.True);
            Assert.That(((RectTransform)document.transform).anchoredPosition.x,
                Is.EqualTo(originalPosition.x).Within(0.01f));
            Assert.That(((RectTransform)document.transform).anchoredPosition.y,
                Is.EqualTo(originalPosition.y).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator Drag_IgnoresWrongAndSecondPointers_AndRestoresExactValueOnDisable() {
            DocumentView document = CreateDocument(Vector2.zero);
            var rect = (RectTransform)document.transform;
            var canvasGroup = document.GetComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            var drag = document.GetComponent<DocumentDragView>();
            yield return null;

            drag.OnBeginDrag(Pointer(3, PointerEventData.InputButton.Left, Vector2.zero));
            drag.OnBeginDrag(Pointer(4, PointerEventData.InputButton.Left, new Vector2(100f, 100f)));
            drag.OnDrag(Pointer(4, PointerEventData.InputButton.Left, new Vector2(50f, 50f)));
            drag.OnEndDrag(Pointer(4, PointerEventData.InputButton.Left, Vector2.zero));
            Assert.That(rect.anchoredPosition.sqrMagnitude, Is.LessThan(0.001f));

            drag.enabled = false;
            Assert.That(canvasGroup.blocksRaycasts, Is.False);
        }

        [UnityTest]
        public IEnumerator Collector_CollectsOnlyOnDrop_ValidatesLayerAndAcceptsUnsignedOnce() {
            DocumentCollector collector = CreateCollector(1 << 8);
            DocumentView wrongLayer = CreateDocument(Vector2.zero, 9);
            DocumentView document = CreateDocument(Vector2.zero, 8);
            int publicationCount = 0;
            SignatureAttempt published = null;
            using IDisposable subscription = collector.Collected.Subscribe(value => {
                publicationCount++;
                published = value;
            });
            yield return null;

            ExecuteEvents.Execute(collector.gameObject, Pointer(wrongLayer.gameObject), ExecuteEvents.dropHandler);
            Assert.That(publicationCount, Is.Zero);
            Assert.That(wrongLayer, Is.Not.Null);

            var wrongType = new GameObject("WrongType") { layer = 8 };
            wrongType.transform.SetParent(_canvasObject.transform, false);
            ExecuteEvents.Execute(collector.gameObject, Pointer(wrongType), ExecuteEvents.dropHandler);
            Assert.That(publicationCount, Is.Zero);

            ExecuteEvents.Execute(document.gameObject, Pointer(document.gameObject), ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(document.gameObject, Pointer(document.gameObject), ExecuteEvents.dragHandler);
            Assert.That(publicationCount, Is.Zero);

            ExecuteEvents.Execute(collector.gameObject, Pointer(document.gameObject), ExecuteEvents.dropHandler);
            Assert.That(publicationCount, Is.EqualTo(1));
            Assert.That(published.Strokes, Is.Empty);
            Assert.That(published.Duration, Is.Zero);

            Assert.That(collector.TryCollect(document, Time.unscaledTime, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator Collector_FinalizesActiveStroke_AcceptsMultipleDocuments_AndAttemptSurvivesDestruction() {
            DocumentCollector collector = CreateCollector(~0);
            DocumentView first = CreateDocument(Vector2.zero, 8);
            DocumentView second = CreateDocument(Vector2.zero, 8);
            SigningField field = first.GetComponentInChildren<SigningField>();
            field.OnPointerDown(Pointer(1, PointerEventData.InputButton.Left, Vector2.zero));
            field.OnDrag(Pointer(1, PointerEventData.InputButton.Left, new Vector2(5f, 5f)));
            yield return null;

            Assert.That(collector.TryCollect(first, Time.unscaledTime, out SignatureAttempt signed), Is.True);
            Assert.That(signed.Strokes.Count, Is.EqualTo(1));
            Assert.That(collector.TryCollect(second, Time.unscaledTime, out SignatureAttempt unsigned), Is.True);
            Assert.That(unsigned.Strokes, Is.Empty);

            UnityEngine.Object.Destroy(first.gameObject);
            yield return null;
            Assert.That(signed.Strokes.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Collector_DisabledReturnsFalse_AndObserverExceptionStillDestroys() {
            DocumentCollector collector = CreateCollector(~0);
            DocumentView disabledAttempt = CreateDocument(Vector2.zero, 8);
            collector.enabled = false;
            Assert.That(collector.TryCollect(disabledAttempt, Time.unscaledTime, out _), Is.False);

            collector.enabled = true;
            DocumentView document = CreateDocument(Vector2.zero, 8);
            collector.Collected.Subscribe(_ => throw new InvalidOperationException("Observer failure"));
            yield return null;

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: Observer failure");
            ExecuteEvents.Execute(collector.gameObject, Pointer(document.gameObject), ExecuteEvents.dropHandler);
            yield return null;
            Assert.That(document == null, Is.True);
        }

        [UnityTest]
        public IEnumerator Collector_CompletionFailureCanBeRetried() {
            DocumentCollector collector = CreateCollector(~0);
            var recorder = new ThrowOnceRecorder();
            DocumentView document = CreateDocument(Vector2.zero, 8, new DocumentViewModel(recorder));
            yield return null;

            Assert.Throws<InvalidOperationException>(() =>
                collector.TryCollect(document, Time.unscaledTime, out _));
            Assert.That(collector.TryCollect(document, Time.unscaledTime, out SignatureAttempt attempt), Is.True);
            Assert.That(attempt, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator Collection_OldEndTimeAfterForcedEnd_CanRetryWithValidTime() {
            DocumentView document = CreateDocument(Vector2.zero, 8);
            SigningField field = document.GetComponentInChildren<SigningField>();
            yield return null;

            field.OnPointerDown(Pointer(1, PointerEventData.InputButton.Left, Vector2.zero));
            field.OnDrag(Pointer(1, PointerEventData.InputButton.Left, new Vector2(10f, 5f)));

            Assert.Throws<ArgumentOutOfRangeException>(() => document.CollectSignature(-1f));
            Assert.That(document.ViewModel.IsStrokeActive, Is.False);
            Assert.That(document.ViewModel.IsSigning, Is.True);

            SignatureAttempt attempt = document.CollectSignature(Time.unscaledTime);
            Assert.That(attempt.Strokes.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator EventSystemRouting_SeparatesBackgroundPadAndReleaseOnlyCollection() {
            DocumentCollector collector = CreateCollector(1 << 8);
            DocumentView document = CreateDocument(Vector2.zero, 8);
            RectTransform documentRect = (RectTransform)document.transform;
            RectTransform padRect = (RectTransform)document.GetComponentInChildren<SigningField>().transform;
            int publicationCount = 0;
            using IDisposable subscription = collector.Collected.Subscribe(_ => publicationCount++);
            yield return null;
            Canvas.ForceUpdateCanvases();

            Vector2 backgroundPosition = RectTransformUtility.WorldToScreenPoint(
                null, documentRect.TransformPoint(new Vector3(80f, 50f)));
            GameObject backgroundHit = RaycastAt(backgroundPosition);
            GameObject backgroundDragHandler = ExecuteEvents.GetEventHandler<IDragHandler>(backgroundHit);
            Assert.That(backgroundDragHandler, Is.EqualTo(document.gameObject));

            PointerEventData backgroundPointer = Pointer(11, PointerEventData.InputButton.Left, backgroundPosition);
            ExecuteEvents.Execute(backgroundDragHandler, backgroundPointer, ExecuteEvents.beginDragHandler);
            backgroundPointer.position += new Vector2(25f, 15f);
            ExecuteEvents.Execute(backgroundDragHandler, backgroundPointer, ExecuteEvents.dragHandler);
            Assert.That(documentRect.anchoredPosition.x, Is.EqualTo(25f).Within(0.01f));
            Assert.That(documentRect.anchoredPosition.y, Is.EqualTo(15f).Within(0.01f));
            ExecuteEvents.Execute(backgroundDragHandler, backgroundPointer, ExecuteEvents.endDragHandler);

            Vector2 padPosition = RectTransformUtility.WorldToScreenPoint(null, padRect.position);
            GameObject padHit = RaycastAt(padPosition);
            GameObject padPointerHandler = ExecuteEvents.GetEventHandler<IPointerDownHandler>(padHit);
            GameObject padDragHandler = ExecuteEvents.GetEventHandler<IDragHandler>(padHit);
            Assert.That(padPointerHandler, Is.EqualTo(padRect.gameObject));
            Assert.That(padDragHandler, Is.EqualTo(padRect.gameObject));
            Vector2 positionBeforeSigning = documentRect.anchoredPosition;

            PointerEventData padPointer = Pointer(12, PointerEventData.InputButton.Left, padPosition);
            ExecuteEvents.Execute(padPointerHandler, padPointer, ExecuteEvents.pointerDownHandler);
            padPointer.position += new Vector2(10f, 5f);
            ExecuteEvents.Execute(padDragHandler, padPointer, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(padPointerHandler, padPointer, ExecuteEvents.pointerUpHandler);
            Assert.That(document.ViewModel.CanCompleteSignature, Is.True);
            Assert.That(documentRect.anchoredPosition, Is.EqualTo(positionBeforeSigning));

            PointerEventData dropPointer = Pointer(13, PointerEventData.InputButton.Left, backgroundPosition);
            dropPointer.pointerDrag = document.gameObject;
            ExecuteEvents.Execute(backgroundDragHandler, dropPointer, ExecuteEvents.beginDragHandler);
            ExecuteEvents.Execute(backgroundDragHandler, dropPointer, ExecuteEvents.dragHandler);
            Assert.That(publicationCount, Is.Zero);

            GameObject releaseHit = RaycastAt(dropPointer.position);
            GameObject dropHandler = ExecuteEvents.GetEventHandler<IDropHandler>(releaseHit);
            Assert.That(dropHandler, Is.EqualTo(collector.gameObject));
            ExecuteEvents.Execute(dropHandler, dropPointer, ExecuteEvents.dropHandler);
            Assert.That(publicationCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CollectorDisabled_DropDoesNothing_ReenableThenDropSucceeds() {
            DocumentCollector collector = CreateCollector(1 << 8);
            DocumentView document = CreateDocument(Vector2.zero, 8);
            int publicationCount = 0;
            using IDisposable subscription = collector.Collected.Subscribe(_ => publicationCount++);
            yield return null;

            collector.enabled = false;
            ExecuteEvents.Execute(collector.gameObject, Pointer(document.gameObject), ExecuteEvents.dropHandler);
            yield return null;
            Assert.That(publicationCount, Is.Zero);
            Assert.That(document, Is.Not.Null);

            collector.enabled = true;
            ExecuteEvents.Execute(collector.gameObject, Pointer(document.gameObject), ExecuteEvents.dropHandler);
            Assert.That(publicationCount, Is.EqualTo(1));
            yield return null;
            Assert.That(document == null, Is.True);
        }

        [UnityTest]
        public IEnumerator Drag_RestoresCanvasGroupWhenDestroyed() {
            DocumentView document = CreateDocument(Vector2.zero);
            var canvasGroup = document.GetComponent<CanvasGroup>();
            var drag = document.GetComponent<DocumentDragView>();
            yield return null;

            drag.OnBeginDrag(Pointer(1, PointerEventData.InputButton.Left, Vector2.zero));
            Assert.That(canvasGroup.blocksRaycasts, Is.False);
            UnityEngine.Object.DestroyImmediate(drag);
            Assert.That(canvasGroup.blocksRaycasts, Is.True);
        }

        private DocumentView CreateDocument(Vector2 position, int layer = 8, DocumentViewModel viewModel = null) {
            var documentObject = new GameObject("Document", typeof(RectTransform));
            documentObject.SetActive(false);
            documentObject.layer = layer;
            documentObject.transform.SetParent(_canvasObject.transform, false);
            var documentRect = (RectTransform)documentObject.transform;
            documentRect.sizeDelta = new Vector2(200f, 150f);
            documentRect.anchoredPosition = position;
            documentObject.AddComponent<Image>();
            documentObject.AddComponent<CanvasGroup>();
            documentObject.AddComponent<DocumentDragView>();
            DocumentView document = documentObject.AddComponent<DocumentView>();

            var padObject = new GameObject("SignaturePad", typeof(RectTransform));
            padObject.transform.SetParent(documentObject.transform, false);
            var padRect = (RectTransform)padObject.transform;
            padRect.sizeDelta = new Vector2(100f, 50f);
            padObject.AddComponent<Image>();

            var graphicObject = new GameObject("SignatureGraphic", typeof(RectTransform));
            graphicObject.transform.SetParent(padObject.transform, false);
            SignatureGraphic graphic = graphicObject.AddComponent<SignatureGraphic>();
            SigningField field = padObject.AddComponent<SigningField>();
            SetField(field, "_signatureGraphic", graphic);
            SetField(document, "_field", field);

            documentObject.SetActive(true);
            document.Init(viewModel ?? new DocumentViewModel());
            return document;
        }

        private DocumentCollector CreateCollector(int acceptedLayers) {
            var collectorObject = new GameObject("Collector", typeof(RectTransform));
            collectorObject.SetActive(false);
            collectorObject.transform.SetParent(_canvasObject.transform, false);
            var collectorRect = (RectTransform)collectorObject.transform;
            collectorRect.anchorMin = Vector2.zero;
            collectorRect.anchorMax = Vector2.one;
            collectorRect.offsetMin = Vector2.zero;
            collectorRect.offsetMax = Vector2.zero;
            collectorObject.transform.SetAsFirstSibling();
            collectorObject.AddComponent<Image>().raycastTarget = true;
            DocumentCollector collector = collectorObject.AddComponent<DocumentCollector>();
            SetField(collector, "_acceptedSourceLayers", (LayerMask)acceptedLayers);
            collectorObject.SetActive(true);
            return collector;
        }

        private PointerEventData Pointer(int id, PointerEventData.InputButton button, Vector2 position) {
            return new PointerEventData(_eventSystem) {
                pointerId = id,
                button = button,
                position = position
            };
        }

        private PointerEventData Pointer(GameObject pointerDrag) {
            return new PointerEventData(_eventSystem) { pointerDrag = pointerDrag };
        }

        private GameObject RaycastAt(Vector2 position) {
            var eventData = new PointerEventData(_eventSystem) { position = position };
            var results = new System.Collections.Generic.List<RaycastResult>();
            _eventSystem.RaycastAll(eventData, results);
            return results.Count > 0 ? results[0].gameObject : null;
        }

        private static void SetField(object target, string name, object value) {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }

        private sealed class ThrowOnceRecorder : ISignatureRecorder {
            private bool _shouldThrow = true;
            public bool IsAttemptActive => true;
            public bool IsStrokeActive => false;
            public void BeginAttempt(float startTime) { }
            public void BeginStroke(SignatureInputPoint firstPoint) { }
            public void AddPoint(SignatureInputPoint point) { }
            public void EndStroke(SignatureInputPoint finalPoint) { }
            public SignatureAttempt CompleteAttempt(float endTime) {
                if (_shouldThrow) {
                    _shouldThrow = false;
                    throw new InvalidOperationException("Expected completion failure.");
                }

                return new SignatureAttempt(Array.Empty<SignatureStrokeAttempt>(), 0f);
            }
            public void CancelAttempt() { }
        }
    }
}
