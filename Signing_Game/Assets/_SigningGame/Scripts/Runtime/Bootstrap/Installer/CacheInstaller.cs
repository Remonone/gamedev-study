using Services.Calculators;
using Services.Locator;

namespace Bootstrap.Installer {
    public class CacheInstaller : MonoInstaller {
        public override void Install(ServiceLocator container) {
            container.Register(new IncomeCacheCalculator());
            container.Register(new GenerationCacheCalculator());
            container.Register(new SignatureCacheCalculator());
            container.Register(new OfficeCacheCalculator());
        }
    }
}
