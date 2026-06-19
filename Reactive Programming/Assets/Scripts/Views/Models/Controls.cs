using System;
using Services.Components;
using Services.Player;
using R3;
using Services.Achievements;
using Types.Enums;

namespace Views.Models {
  public class Controls : IDisposable {
        private Storage _storage;
        
        private CompositeDisposable _disposable = new();

        public BuildingShopTabViewModel BuildingShopTab = new();
        public UpgradesTabViewModel UpgradesTab = new();
        public AchievementsTabViewModel AchievementsTab;
        public ArtifactsTabViewModel ArtifactsTab = new();

        public ReactiveProperty<long> DocumentsCount = new ReactiveProperty<long>(0);
        public ReactiveProperty<long> CasesCount = new ReactiveProperty<long>(0);
        public ReactiveProperty<long> FiresCount = new ReactiveProperty<long>(0);
        public ReactiveProperty<long> ProtectionsCount = new ReactiveProperty<long>(0);
        public ReactiveProperty<long> CuresCount = new ReactiveProperty<long>(0);
        public ReactiveProperty<long> ArchivesCount = new ReactiveProperty<long>(0);

        public Controls() {
            _storage = ServiceLocator.Instance.GetService<Storage>();

            AchievementsTab = new AchievementsTabViewModel(ServiceLocator.Instance.GetService<AchievementService>());

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
        }
  }
}
