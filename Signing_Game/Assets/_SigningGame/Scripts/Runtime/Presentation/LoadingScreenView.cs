using R3;
using Services;
using Services.Locator;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation {
    public sealed class LoadingScreenView : MonoBehaviour {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private Slider _progress;
        [SerializeField] private TextMeshProUGUI _status;

        private readonly CompositeDisposable _subscriptions = new();

        private void Start() {
            ServiceLocator locator = ServiceLocator.Application;
            if (locator == null || !locator.TryGet(out SceneFlowService flow)) return;
            flow.Loading.Subscribe(SetVisible).AddTo(_subscriptions);
            flow.Progress.Subscribe(value => _progress.SetValueWithoutNotify(value)).AddTo(_subscriptions);
            flow.LastError.Subscribe(OnErrorChanged).AddTo(_subscriptions);
        }

        public void ShowFatal(string message) {
            SetVisible(true);
            if (_status != null) _status.text = string.IsNullOrWhiteSpace(message) ? "Fatal startup error." : message;
        }

        private void SetVisible(bool visible) {
            if (_canvasGroup == null) return;
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
            if (visible && _status != null && string.IsNullOrWhiteSpace(_status.text)) _status.text = "Loading...";
        }

        private void OnErrorChanged(string error) {
            if (_status == null) return;
            _status.text = string.IsNullOrWhiteSpace(error) ? "Loading..." : error;
        }

        private void OnDestroy() => _subscriptions.Dispose();
    }
}
