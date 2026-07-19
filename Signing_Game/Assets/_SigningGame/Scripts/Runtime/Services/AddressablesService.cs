using Services.Locator;
using UnityEngine;

namespace Services {
    public class AddressablesService : IService, IInitialize {
        public void Dispose() {
            
        }

        Awaitable IInitialize.InitializeAsync(IServiceScope scope) {
            var source = new AwaitableCompletionSource();
            Awaitable awaitable = source.Awaitable;
            source.SetResult();
            return awaitable;
        }
    }
}
