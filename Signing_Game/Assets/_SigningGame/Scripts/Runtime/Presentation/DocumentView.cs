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

        private DocumentViewModel _viewModel;

        public DocumentViewModel ViewModel => _viewModel;

        public void Init(DocumentViewModel viewModel) {
            DocumentViewModel nextViewModel = viewModel
                ?? throw new ArgumentNullException(nameof(viewModel));
            if (ReferenceEquals(_viewModel, nextViewModel))
                throw new InvalidOperationException("DocumentView cannot be reinitialized with the same ViewModel instance.");

            _field.Clear();
            _viewModel = nextViewModel;
            RefreshView();
            gameObject.SetActive(true);
        }

        private void RefreshView() {
            ulong seed = _viewModel.TextSeed;
            var document = DeterministicDocumentGenerator.Generate(seed, _textSettings);
            _text.text = TmpDocumentFormatter.Format(document);
            _header.color = _viewModel.HeaderColor;
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
        }
    }
}
