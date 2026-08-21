using System;
using R3;
using Services;

namespace Presentation {
    public sealed class GameMenuTabViewModel : IDisposable {
        private readonly SignatureGuidanceDocumentProducer _guidanceProducer;
        private readonly SaveService _saveService;
        private readonly SceneFlowService _sceneFlow;
        private readonly ReactiveProperty<bool> _settingsVisible = new(false);

        public Observable<bool> SettingsVisible => _settingsVisible;

        public GameMenuTabViewModel(
            SignatureGuidanceDocumentProducer guidanceProducer,
            SaveService saveService,
            SceneFlowService sceneFlow) {
            _guidanceProducer = guidanceProducer
                ?? throw new ArgumentNullException(nameof(guidanceProducer));
            _saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
            _sceneFlow = sceneFlow ?? throw new ArgumentNullException(nameof(sceneFlow));
        }

        public void OpenSettings() => _settingsVisible.Value = true;
        public void CloseSettings() => _settingsVisible.Value = false;
        public void RequestSignatureGuidance() => _guidanceProducer.Request();

        public bool SaveAndExit() {
            if (!_saveService.SaveToFile()) return false;

            _sceneFlow.OpenMainMenu();
            return true;
        }

        public void Dispose() => _settingsVisible.Dispose();
    }
}
