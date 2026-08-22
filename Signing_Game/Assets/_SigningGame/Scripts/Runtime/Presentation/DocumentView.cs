using System;
using Data.Documents;
using Data.Input;
using Data.Sound;
using R3;
using Services;
using Services.Locator;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;
using Utils.Text;
using Utils.Text.Generator;

namespace Presentation {
    public class DocumentView : MonoBehaviour {
        [SerializeField] private SigningField _field;
        [SerializeField] private Image _header;
        [SerializeField] private TextMeshProUGUI _text;
        [SerializeField] private DocumentTextSettings _textSettings;
        [SerializeField] private TextMeshProUGUI _headerTitle;
        [SerializeField] private Image _headerIcon;
        [SerializeField] private TextMeshProUGUI _profileText;
        [SerializeField] private TextMeshProUGUI _amountText;
        [SerializeField] private TextMeshProUGUI _internalMultiplierText;
        [SerializeField] private AudioCue _pickupCue;
        [SerializeField] private AudioCue _dropCue;
        [SerializeField] private AudioCue _drawCue;

        private DocumentViewModel _viewModel;
        private DispensedDocumentPresentation _presentation;
        private AudioService _audioService;
        private StampGraphic _stampGraphic;
        private readonly Vector3[] _documentWorldCorners = new Vector3[4];

        private double _soundCooldown;

        public DocumentViewModel ViewModel => _viewModel;
        internal bool CanReceiveStamp => _presentation != null &&
                                          DocumentKind.Normal.Equals(_presentation.Kind);

        public void ShowPreview(DispensedDocumentPresentation presentation) {
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            if (_viewModel != null) throw new InvalidOperationException("A bound document cannot become a preview again.");

            _field.HideGuidance();
            _field.Clear();
            _stampGraphic.Clear();
            _presentation = presentation;
            RefreshView();
            gameObject.SetActive(true);
        }

        public void Init(DocumentViewModel viewModel, DispensedDocumentPresentation presentation,
            SignatureGuidanceSnapshot guidance) {
            DocumentViewModel nextViewModel = viewModel
                ?? throw new ArgumentNullException(nameof(viewModel));
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            if (_viewModel != null) throw new InvalidOperationException("DocumentView can only be bound once.");

            _field.Clear();
            _stampGraphic.Clear();
            _presentation = presentation;
            _viewModel = nextViewModel;
            RefreshView();
            if (!_field.ConfigureGuidance(guidance)) {
                throw new InvalidOperationException("Document guidance geometry cannot be mapped to the signing field.");
            }
            gameObject.SetActive(true);
        }

        public void Init(DocumentViewModel viewModel, DispensedDocumentPresentation presentation) {
            Init(viewModel, presentation, null);
        }

        private void RefreshView() {
            if (_presentation == null) throw new InvalidOperationException("Document presentation is required.");
            ulong seed = _presentation.TextSeed;
            var document = DeterministicDocumentGenerator.Generate(seed, _textSettings);
            _text.text = TmpDocumentFormatter.Format(document);
            if (DocumentKind.Normal.Equals(_presentation.Kind))
                _header.color = _presentation.HeaderColor;
            string headerText = _presentation.RequiresStamp ? "STAMP REQUIRED" : _presentation.HeaderText;
            SetOptionalText(_headerTitle, headerText);
            SetOptionalText(_profileText, _presentation.ProfileText);
            SetOptionalText(_amountText, _presentation.AmountText);
            SetOptionalText(_internalMultiplierText, _presentation.InternalMultiplierText);
            if (_headerIcon != null) {
                bool hasIcon = _presentation.HeaderIcon != null;
                _headerIcon.sprite = _presentation.HeaderIcon;
                _headerIcon.preserveAspect = true;
                _headerIcon.gameObject.SetActive(hasIcon);
            }
        }

        private static void SetOptionalText(TextMeshProUGUI target, string value) {
            if (target == null) return;
            bool hasValue = !string.IsNullOrWhiteSpace(value);
            target.text = value ?? string.Empty;
            target.gameObject.SetActive(hasValue);
        }

        public SignatureAttempt CollectSignature(float endTime) {
            if (_viewModel == null) {
                throw new InvalidOperationException("DocumentView must be initialized before collection.");
            }

            if (float.IsNaN(endTime) || float.IsInfinity(endTime)) {
                throw new ArgumentOutOfRangeException(nameof(endTime), "Time must be finite.");
            }

            _field.FinishActiveStrokeForCollection(endTime);
            return _viewModel.CollectSignature(endTime);
        }

        internal bool TryGetScreenRect(Camera eventCamera, out Rect screenRect) {
            screenRect = default;
            if (_viewModel == null || !CanReceiveStamp || !isActiveAndEnabled) return false;

            RectTransform documentRect = (RectTransform)transform;
            documentRect.GetWorldCorners(_documentWorldCorners);
            Vector2 first = RectTransformUtility.WorldToScreenPoint(eventCamera, _documentWorldCorners[0]);
            float xMin = first.x;
            float xMax = first.x;
            float yMin = first.y;
            float yMax = first.y;
            for (int index = 1; index < _documentWorldCorners.Length; index++) {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(
                    eventCamera,
                    _documentWorldCorners[index]);
                xMin = Mathf.Min(xMin, point.x);
                xMax = Mathf.Max(xMax, point.x);
                yMin = Mathf.Min(yMin, point.y);
                yMax = Mathf.Max(yMax, point.y);
            }

            screenRect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return screenRect.width > 0f && screenRect.height > 0f;
        }

        internal bool TryPlaceStamp(Rect stampScreenRect, Camera eventCamera) {
            if (_viewModel == null || !_stampGraphic ||
                !TryGetScreenRect(eventCamera, out Rect documentScreenRect)) return false;

            float xMin = Mathf.Max(stampScreenRect.xMin, documentScreenRect.xMin);
            float yMin = Mathf.Max(stampScreenRect.yMin, documentScreenRect.yMin);
            float xMax = Mathf.Min(stampScreenRect.xMax, documentScreenRect.xMax);
            float yMax = Mathf.Min(stampScreenRect.yMax, documentScreenRect.yMax);
            if (xMax <= xMin || yMax <= yMin) return false;

            RectTransform stampRect = _stampGraphic.rectTransform;
            Vector2 localMin;
            Vector2 localMax;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    stampRect,
                    new Vector2(xMin, yMin),
                    eventCamera,
                    out localMin) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    stampRect,
                    new Vector2(xMax, yMax),
                    eventCamera,
                    out localMax)) return false;

            Rect localRect = Rect.MinMaxRect(
                Mathf.Min(localMin.x, localMax.x),
                Mathf.Min(localMin.y, localMax.y),
                Mathf.Max(localMin.x, localMax.x),
                Mathf.Max(localMin.y, localMax.y));
            if (!_stampGraphic.TryAddStamp(localRect)) return false;

            _viewModel.MarkStamped();
            return true;
        }

        private void Awake() {
            _stampGraphic = CreateStampGraphic();
            ServiceLocator.For(this).TryGet(out _audioService);
            _field.OnInput.Subscribe(OnDraw).AddTo(this);

            if (TryGetComponent(out DocumentDragView dragView)) {
                dragView.IsDragging.Subscribe(OnDraggingChanged).AddTo(this);
            }
        }

        private void OnDraggingChanged(bool isDragging) {
            _audioService?.PlaySfx(isDragging ? _pickupCue : _dropCue);
        }

        private void OnDraw(SignatureInputEvent e) {
            if (_viewModel == null) return;

            if (e.Type == SignatureInputEventType.Cleared) {
                _viewModel.CancelSignature();
                return;
            }

            SignatureInputPoint point = new(e.NormalizedPosition, e.Timestamp);

            switch (e.Type) {
                case SignatureInputEventType.StrokeStarted: {
                    _viewModel.StartStroke(point);
                    break;
                }
                case SignatureInputEventType.PointAdded: {
                    _viewModel.AddPoint(point);
                    if (_soundCooldown < Time.timeAsDouble) {
                        _audioService?.PlaySfx(_drawCue);
                        _soundCooldown = Time.timeAsDouble + (_drawCue.Clips.Length > 0 ? _drawCue.Clips[0].length : 0) * 0.85d;
                    }
                    break;
                }
                case SignatureInputEventType.StrokeEnded: {
                    _viewModel.FinishStroke(point);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnDestroy() {
            _viewModel?.Dispose();
            _viewModel = null;
            _presentation = null;
        }

        private StampGraphic CreateStampGraphic() {
            var stampObject = new GameObject("Stamp Graphic", typeof(RectTransform));
            stampObject.transform.SetParent(transform, false);
            RectTransform rect = (RectTransform)stampObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            StampGraphic graphic = stampObject.AddComponent<StampGraphic>();
            graphic.color = new Color(0.72f, 0.08f, 0.08f, 0.42f);
            graphic.raycastTarget = false;
            graphic.transform.SetAsLastSibling();
            return graphic;
        }
    }
}
