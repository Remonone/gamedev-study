using Contracts;
using Services;
using Services.Locator;

namespace Bootstrap.Installer {
    public class GameSceneInstaller : MonoInstaller {
        public override void Install(ServiceLocator container) {
            var signaturePreprocessor = new SignaturePreprocessor();
            var compiler = new SignaturePresetCompiler();
            var repository = new SignaturePresetRepository();
            var resolver = new RuleResolver();
            var matcher = new SignatureMatcher();
            var evaluator = new SignatureEvaluator();

            RegisterShared(container, signaturePreprocessor, typeof(ISignaturePreprocessor));
            RegisterShared(container, compiler, typeof(ISignaturePresetCompiler));
            RegisterShared(container, repository, typeof(ISignaturePresetRepository));
            RegisterShared(container, resolver, typeof(ISignatureRulesResolver));
            RegisterShared(container, matcher, typeof(ISignatureMatcher));
            RegisterShared(container, evaluator, typeof(ISignatureEvaluator));
            RegisterShared(container, new MoneyAggregator(), typeof(IMoneyAggregator));
            container.Register(new WalletService());
            container.Register(new DifficultyProfileEvaluator());
            container.Register(new PlayerStatStash());
            container.Register(new PlayerSignatureAcceptor());
        }

        
    }
}
