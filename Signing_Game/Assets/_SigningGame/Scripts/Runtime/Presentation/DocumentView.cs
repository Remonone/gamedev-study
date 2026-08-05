using System;
using Data.Documents;
using Data.Input;
using R3;
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

        private DocumentViewModel _viewModel;
        private DispensedDocumentPresentation _presentation;

        public DocumentViewModel ViewModel => _viewModel;

        public void ShowPreview(DispensedDocumentPresentation presentation) {
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            if (_viewModel != null) throw new InvalidOperationException("A bound document cannot become a preview again.");

            _field.Clear();
            _presentation = presentation;
            RefreshView();
            gameObject.SetActive(true);
        }

        public void Init(DocumentViewModel viewModel, DispensedDocumentPresentation presentation) {
            DocumentViewModel nextViewModel = viewModel
                ?? throw new ArgumentNullException(nameof(viewModel));
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            if (_viewModel != null) throw new InvalidOperationException("DocumentView can only be bound once.");

            _field.Clear();
            _presentation = presentation;
            _viewModel = nextViewModel;
            RefreshView();
            gameObject.SetActive(true);
        }

        private void RefreshView() {
            if (_presentation == null) throw new InvalidOperationException("Document presentation is required.");
            ulong seed = _presentation.TextSeed;
            var document = DeterministicDocumentGenerator.Generate(seed, _textSettings);
            _text.text = TmpDocumentFormatter.Format(document);
            if (DocumentKind.Normal.Equals(_presentation.Kind))
                _header.color = _presentation.HeaderColor;
            SetOptionalText(_headerTitle, _presentation.HeaderText);
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

        private void Awake() {
            _field.OnInput.Subscribe(OnDraw).AddTo(this);
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
    }
}
