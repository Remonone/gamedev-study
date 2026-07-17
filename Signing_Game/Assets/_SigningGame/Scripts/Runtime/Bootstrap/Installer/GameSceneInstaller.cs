using Services;
using Services.Locator;

namespace Bootstrap.Installer {
    public class GameSceneInstaller : MonoInstaller {
        public override void Install(ServiceLocator container) {
            var signaturePreprocessor = new SignaturePreprocessor();
            container.Register(signaturePreprocessor);
        }
    }
}