using System;
using System.Collections.Generic;
using Services;
using Services.Locator;
using UnityEngine;

namespace Bootstrap.Installer {
    public class GameSceneMonoBehaviorInstaller : MonoInstaller {
        [SerializeField, InspectorName("Services to register")]
        private List<GameObject> _services = new();
        public override void Install(ServiceLocator container) {
            foreach (var serviceObj in _services) {
                if (!serviceObj.TryGetComponent(out IService service))
                    throw new InvalidOperationException($"Service {serviceObj.name} does not implement IService.");
                container.Register(service);
            }
        }
    }
}