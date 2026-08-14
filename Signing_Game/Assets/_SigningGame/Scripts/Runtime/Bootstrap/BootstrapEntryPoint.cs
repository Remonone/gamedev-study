using Cysharp.Threading.Tasks;
using Presentation;
using Services;
using Services.Locator;
using UnityEngine;

namespace Bootstrap {
    public sealed class BootstrapEntryPoint : MonoBehaviour {
        [SerializeField] private LoadingScreenView _loadingScreen;

        private AudioSettingsService _audioSettings;

        private async void Start() {
            ServiceLocator locator = ServiceLocator.Application;
            await UniTask.WaitUntil(() => locator != null && locator.IsInitializationComplete,
                cancellationToken: this.GetCancellationTokenOnDestroy());
            if (locator.InitializationException != null) {
                _loadingScreen?.ShowFatal($"Global initialization failed: {locator.InitializationException.Message}");
                return;
            }

            locator.TryGet(out _audioSettings);
            locator.Get<SceneFlowService>().OpenMainMenu();
        }

        private void OnApplicationPause(bool paused) {
            if (paused) _audioSettings?.Flush();
        }

        private void OnApplicationQuit() => _audioSettings?.Flush();
    }
}
