using Contracts;
using Data.Cache;
using Data.Modifiers;
using Data.Modifiers.Providers;
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
            var modifierStorage = new ModifierStorage();
            modifierStorage.RegisterProvider(new UpgradeModifierProvider());
            modifierStorage.RegisterProvider(new PracticeModifierProvider());
            modifierStorage.RegisterProvider(new BillModifierProvider());
            container.Register(modifierStorage);
            container.Register<IModifierService>(new ModifierService());
            container.Register(new CacheVersionService(), typeof(ICacheVersionProvider), typeof(ICacheInvalidator));
            container.Register(new MoneyAggregator());
            container.Register(new DocumentGeneratorService());
            container.Register(new WalletService());
            container.Register(new UpgradeService());
            container.Register(new GameStatisticsService());
            container.Register(new UnlockService());
            container.Register(new UpgradeTreeService());
            container.Register(new PlayerStatStash());
            container.Register(new BankService());
            container.Register(new AcceptedNormalDocumentService());
            container.Register(new NormalDocumentProducer());
            container.Register(new UpgradeDocumentProducer());
            container.Register(new OfficeService());
            container.Register(new ClerkHireDocumentProducer());
            container.Register(new ClerkSalaryReviewDocumentProducer());
            container.Register(new BillService());
            var billDocumentProducer = new BillDocumentProducer();
            container.Register(billDocumentProducer, typeof(IDocumentProducer));
            container.Register(new ResearchService());
            var practiceDocumentProducer = new PracticeDocumentProducer();
            container.Register(practiceDocumentProducer, typeof(IDocumentProducer));
            container.Register(new PlayerSignatureAcceptor());
        }

        
    }
}
