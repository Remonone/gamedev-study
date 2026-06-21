using System;
using System.Collections.Generic;
using System.Linq;
using Save;
using UnityEngine;

namespace Services.Components {
    public abstract class ServiceInstaller : MonoBehaviour {

        private SaveManager _saveManager;
        
        protected SaveManager SaveManager => _saveManager;

        private List<IService> _services = new();
        
        private void Awake() {
            _saveManager = new SaveManager();
            InstallServices();
            RestoreState();
            StartServices();
            AfterInstallation();
        }

        private void StartServices() {
            foreach (var service in _services.OfType<IStartable>()) {
                service.StartService();
            }
        }

        protected abstract void AfterInstallation();

        protected abstract void InstallServices();

        protected void RegisterService(IService service) {
            ServiceLocator.Instance.RegisterService(service);
            if (service is ISaveable saveable) {
                _saveManager.Register(saveable);
            }
            _services.Add(service);
        }

        private void OnDestroy() {
            foreach (var service in _services.OfType<IDisposable>()) {
                service.Dispose();
            }
        }
        
        void RestoreState() {
            _saveManager.Load();
        }
    }
}
