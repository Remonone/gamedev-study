using UnityEngine;

namespace Components {
    public abstract class ServiceInstaller : MonoBehaviour {

        private void Awake() {
            InstallServices();
        }

        protected abstract void InstallServices();
    }
}
