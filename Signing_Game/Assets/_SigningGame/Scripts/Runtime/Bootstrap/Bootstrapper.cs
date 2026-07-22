using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Services.Locator;
using UnityEngine;

namespace Bootstrap {

    [DisallowMultipleComponent]
    [RequireComponent(typeof(ServiceLocator))]
    public abstract class Bootstrapper : MonoBehaviour {
        [SerializeField] private List<MonoInstaller> _installers = new();

        private ServiceLocator _container;

        public ServiceLocator Container => _container ??= GetComponent<ServiceLocator>();

        bool _hasBeenBootstrapped;

        private async void Awake() => await BootstrapOnDemand();

        public async UniTask BootstrapOnDemand() {
            if (_hasBeenBootstrapped) return;
            _hasBeenBootstrapped = true;

            // Registration runs synchronously (before the first await), so callers
            // that ignore the returned Awaitable still get a fully populated scope.
            Configure();
            foreach (var installer in _installers) {
                installer.Install(Container);
            }

            await Container.InitializeScopeAsync();
        }

        protected abstract void Configure();
    }
}
