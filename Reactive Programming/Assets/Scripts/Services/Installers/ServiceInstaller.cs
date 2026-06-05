using Save;
using UnityEngine;

namespace Components {
    public abstract class ServiceInstaller : MonoBehaviour {

        private SaveManager _saveManager;
        
        protected SaveManager SaveManager => _saveManager;
        
        private void Awake() {
            _saveManager = new SaveManager();
            InstallServices();
            RestoreState();
            AfterInstallation();
        }

        protected abstract void AfterInstallation();

        protected abstract void InstallServices();

        protected void RegisterService(IService service) {
            ServiceLocator.Instance.RegisterService(service);
            if (service is ISaveable saveable) {
                _saveManager.Register(saveable);
            }
        }
        
        void RestoreState() {
            _saveManager.Load();
        }
    }
}
