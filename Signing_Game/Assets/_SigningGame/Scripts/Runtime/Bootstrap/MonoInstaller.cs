using Services.Locator;
using UnityEngine;

namespace Bootstrap {
    public abstract class MonoInstaller : MonoBehaviour {
        public abstract void Install(ServiceLocator container);
    }
}
