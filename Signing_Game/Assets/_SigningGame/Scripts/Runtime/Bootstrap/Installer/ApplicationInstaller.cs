using Contracts;
using Services;
using Services.Locator;
using System;
using UnityEngine;

namespace Bootstrap.Installer {
    public class ApplicationInstaller : MonoInstaller {
        [SerializeField] private AudioService _audioService;

        public override void Install(ServiceLocator container) {
            if (_audioService == null) {
                throw new InvalidOperationException("ApplicationInstaller requires a persistent AudioService reference.");
            }

            container.Register<IAssetProvider>(new AddressablesService());
            container.Register(_audioService);
            container.Register(new AudioSettingsService(_audioService));
            var session = new GameSessionService();
            container.Register(session);
            container.Register(new SceneFlowService(session));
        }
    }
}
