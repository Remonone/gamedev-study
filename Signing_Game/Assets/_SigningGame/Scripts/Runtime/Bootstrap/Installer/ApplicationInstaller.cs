using Contracts;
using Services;
using Services.Locator;

namespace Bootstrap.Installer {
    public class ApplicationInstaller : MonoInstaller {
        public override void Install(ServiceLocator container) {
            RegisterShared(container, new AddressablesService(), typeof(IAssetProvider));
        }
    }
}