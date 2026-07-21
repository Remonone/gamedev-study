using System;
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
            var convertor = new RewardConvertor();

            RegisterShared(container, signaturePreprocessor, typeof(ISignaturePreprocessor));
            RegisterShared(container, compiler, typeof(ISignaturePresetCompiler));
            RegisterShared(container, repository, typeof(ISignaturePresetRepository));
            RegisterShared(container, resolver, typeof(ISignatureRulesResolver));
            RegisterShared(container, matcher, typeof(ISignatureMatcher));
            RegisterShared(container, evaluator, typeof(ISignatureEvaluator));
            container.Register(convertor);
        }

        private static void RegisterShared<T>(ServiceLocator container, T service, Type contract) where T : IService {
            container.Register(service);
            container.Register(contract, service);
        }
    }
}
