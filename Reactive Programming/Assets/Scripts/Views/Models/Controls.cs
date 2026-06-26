using System;
using Services.Components;
using Services.Events;
using Services.Player;
using Services;
using R3;
using Services.Achievements;
using Types.Enums;
using Types.Values;

namespace Views.Models {
  public class Controls : IDisposable {
        private Storage _storage;
        
        private CompositeDisposable _disposable = new();

        public BuildingShopTabViewModel BuildingShopTab = new();
        public UpgradesTabViewModel UpgradesTab = new();
        public AchievementsTabViewModel AchievementsTab;
        public ArtifactsTabViewModel ArtifactsTab;
        public ResearchTabViewModel ResearchTab;
        public PracticeRewardPopupViewModel PracticeRewardPopup;
        public GlobalEventIndicatorViewModel GlobalEventIndicator;

        public ReactiveProperty<Value> DocumentsCount = new(Value.Zero);
        public ReactiveProperty<Value> CasesCount = new(Value.Zero);
        public ReactiveProperty<Value> FiresCount = new(Value.Zero);
        public ReactiveProperty<Value> ProtectionsCount = new(Value.Zero);
        public ReactiveProperty<Value> CuresCount = new(Value.Zero);
        public ReactiveProperty<Value> ArchivesCount = new(Value.Zero);

        public Controls(Storage storage) {
            _storage = storage;

            AchievementsTab = new AchievementsTabViewModel(ServiceLocator.Instance.GetService<AchievementService>());
            var researchService = ServiceLocator.Instance.GetService<ResearchService>();
            var practiceService = ServiceLocator.Instance.GetService<PracticeService>();
            ArtifactsTab = new ArtifactsTabViewModel(practiceService);
            ResearchTab = new ResearchTabViewModel(researchService);
            PracticeRewardPopup = new PracticeRewardPopupViewModel(practiceService, researchService);
            GlobalEventIndicator = new GlobalEventIndicatorViewModel(ServiceLocator.Instance.GetService<GlobalEventService>());

            _storage.ObserveByType(GovernmentInteractionType.MayorOffice)
                .Subscribe(update => DocumentsCount.Value = update)
                .AddTo(_disposable);
            
            _storage.ObserveByType(GovernmentInteractionType.Court)
                .Subscribe(update => CasesCount.Value = update)
                .AddTo(_disposable);

            _storage.ObserveByType(GovernmentInteractionType.FireFighterStation)
                .Subscribe(update => FiresCount.Value = update)
                .AddTo(_disposable);

            _storage.ObserveByType(GovernmentInteractionType.PoliceStation)
                .Subscribe(update => ProtectionsCount.Value = update)
                .AddTo(_disposable);

            _storage.ObserveByType(GovernmentInteractionType.Hospital)
                .Subscribe(update => CuresCount.Value = update)
                .AddTo(_disposable);
            
            _storage.ObserveByType(GovernmentInteractionType.Archive)
                .Subscribe(update => ArchivesCount.Value = update)
                .AddTo(_disposable);
        }


        public void Dispose() {
            _disposable.Dispose();
            UpgradesTab.Dispose();
            ArtifactsTab.Dispose();
            ResearchTab.Dispose();
            PracticeRewardPopup.Dispose();
            GlobalEventIndicator.Dispose();
        }
  }
}
