using Contracts;
using Services;
using Services.Locator;

namespace Bootstrap.Installer {
    public class GameSceneInstaller : MonoInstaller {
        public override void Install(ServiceLocator container) {
            container.Register<ISignaturePreprocessor>(new SignaturePreprocessor());
            container.Register<ISignaturePresetCompiler>(new SignaturePresetCompiler());
            container.Register<ISignaturePresetRepository>(new SignaturePresetRepository());
            container.Register<ISignatureRulesResolver>(new RuleResolver());
            container.Register<ISignatureMatcher>(new SignatureMatcher());
            container.Register<ISignatureEvaluator>(new SignatureEvaluator());
            container.Register(new DocumentSpawnerService());
            container.Register(new MoneyAggregator());
            container.Register(new DocumentGeneratorService());
            container.Register(new WalletService());
            container.Register(new DifficultyProfileEvaluator());
            container.Register(new PlayerStatStash());
            container.Register(new PlayerSignatureAcceptor());
        }

        
    }
}
