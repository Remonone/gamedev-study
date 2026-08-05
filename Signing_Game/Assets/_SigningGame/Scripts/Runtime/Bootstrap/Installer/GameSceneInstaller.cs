using Contracts;
using Data.Cache;
using Data.Modifiers;
using Services;
using Services.Locator;

namespace Bootstrap.Installer {
    public class GameSceneInstaller : MonoInstaller {
        public override void Install(ServiceLocator container) {
            var saveService = new SaveService();
            container.Register(saveService);
            container.Register(new AutoSaveService(saveService));
            container.Register<ISignaturePreprocessor>(new SignaturePreprocessor());
            container.Register<ISignaturePresetCompiler>(new SignaturePresetCompiler());
            container.Register<ISignaturePresetRepository>(new SignaturePresetRepository());
            container.Register<ISignatureRulesResolver>(new RuleResolver());
            container.Register<ISignatureMatcher>(new SignatureMatcher());
            container.Register<ISignatureEvaluator>(new SignatureEvaluator());
            container.Register(new ModifierStorage());
            container.Register<IModifierService>(new ModifierService());
            container.Register(new CacheVersionService(), typeof(ICacheVersionProvider), typeof(ICacheInvalidator));
            container.Register(new DocumentSpawnerService());
            container.Register(new MoneyAggregator());
            container.Register(new DocumentGeneratorService());
            container.Register(new WalletService());
            container.Register(new UpgradeService());
            container.Register(new GameStatisticsService());
            container.Register(new UnlockService());
            container.Register(new UpgradeTreeService());
            container.Register(new DifficultyProfileEvaluator());
            container.Register(new PlayerStatStash());
            container.Register(new NormalDocumentProducer());
            container.Register(new UpgradeDocumentProducer());
            container.Register(new OfficeService());
            container.Register(new ClerkHireDocumentProducer());
            container.Register(new ClerkSalaryReviewDocumentProducer());
            container.Register(new PlayerSignatureAcceptor());
        }

        
    }
}
