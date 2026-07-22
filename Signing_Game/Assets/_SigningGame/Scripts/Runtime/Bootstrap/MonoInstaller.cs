using System;
using Services;
using Services.Locator;
using UnityEngine;

namespace Bootstrap {
    public abstract class MonoInstaller : MonoBehaviour {
        public abstract void Install(ServiceLocator container);
        
        protected static void RegisterShared<T>(ServiceLocator container, T service, Type contract) where T : IService {
            container.Register(service);
            container.Register(contract, service);
        }
    }
}
