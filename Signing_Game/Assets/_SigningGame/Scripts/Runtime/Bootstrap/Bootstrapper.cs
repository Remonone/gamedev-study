using System;
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

        private bool _hasBeenBootstrapped;
        private bool _isBootstrapping;

        private async void Awake() => await BootstrapOnDemand();

        public async UniTask BootstrapOnDemand() {
            if (_hasBeenBootstrapped) return;
            if (_isBootstrapping) {
                await UniTask.WaitUntil(() => !_isBootstrapping);
                return;
            }

            _isBootstrapping = true;
            Container.BeginInitialization();
            try {
                // Registration runs synchronously (before the first await), so callers
                // that ignore the returned Awaitable still get a fully populated scope.
                Configure();
                foreach (MonoInstaller installer in _installers) {
                    if (installer == null) throw new InvalidOperationException("Bootstrapper contains a missing installer reference.");
                    installer.Install(Container);
                }

                await Container.InitializeScopeAsync();
                Container.CompleteInitialization();
            } catch (Exception exception) {
                Container.FailInitialization(exception);
                Debug.LogException(exception, this);
            } finally {
                _hasBeenBootstrapped = true;
                _isBootstrapping = false;
            }
        }

        protected abstract void Configure();
    }
}
