using System;
using R3;
using Services;

namespace Presentation {
    public sealed class MainMenuViewModel : IDisposable {
        private readonly SceneFlowService _sceneFlow;
        private readonly ReactiveProperty<bool> _settingsVisible = new(false);

        public Observable<bool> SettingsVisible => _settingsVisible;
        public Observable<bool> Loading => _sceneFlow.Loading;
        public Observable<string> LastError => _sceneFlow.LastError;

        public MainMenuViewModel(SceneFlowService sceneFlow) {
            _sceneFlow = sceneFlow ?? throw new ArgumentNullException(nameof(sceneFlow));
        }

        public void Play() => _sceneFlow.Play();
        public void Quit() => _sceneFlow.Quit();
        public void OpenSettings() => _settingsVisible.Value = true;
        public void CloseSettings() => _settingsVisible.Value = false;
        public void Dispose() => _settingsVisible.Dispose();
    }
}
