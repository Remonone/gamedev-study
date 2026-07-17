using Services.Locator;
using UnityEngine;

namespace Services {
    public class AddressablesService : IService, IInitialize {
        public void Dispose() {
            
        }

        Awaitable IInitialize.InitializeAsync(ServiceLocator container) {
            return null;
        }
    }
}