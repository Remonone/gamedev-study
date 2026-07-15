using System;
using Data.Input;
using R3;
using UI;
using UnityEngine;

namespace Presentation {
    public class DocumentView : MonoBehaviour {
        [SerializeField] private SigningField _field;

        private DocumentViewModel _viewModel;

        public DocumentViewModel ViewModel => _viewModel;

        public void Init(DocumentViewModel viewModel) {
            DocumentViewModel nextViewModel = viewModel
                ?? throw new ArgumentNullException(nameof(viewModel));

            _field.Clear();
            _viewModel = nextViewModel;
            gameObject.SetActive(true);
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
    }
}
