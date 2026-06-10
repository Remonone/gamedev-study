using System;
using Components;
using Services.Player;
using R3;
using Types;

namespace Views.Models {
  public class Controls : IDisposable {
        private Storage _storage;
        
        private CompositeDisposable _disposable = new();

        public BuildingShopTabViewModel BuildingShopTab = new();
        public UpgradesTabViewModel UpgradesTab = new();
        public AchievementsTabViewModel AchievementsTab = new();
        public ArtifactsTabViewModel ArtifactsTab = new();

        public ReactiveProperty<long> DocumentsCount = new ReactiveProperty<long>(0);
        public ReactiveProperty<long> CasesCount = new ReactiveProperty<long>(0);
        public ReactiveProperty<long> FiresCount = new ReactiveProperty<long>(0);
        public ReactiveProperty<long> ProtectionsCount = new ReactiveProperty<long>(0);
        public ReactiveProperty<long> CuresCount = new ReactiveProperty<long>(0);
        public ReactiveProperty<long> ArchivesCount = new ReactiveProperty<long>(0);

        public Controls() {
            _storage = ServiceLocator.Instance.GetService<Storage>();

            _storage.ObserveByType(StructureType.MayorOffice)
                .Subscribe(update => DocumentsCount.Value = update)
                .AddTo(_disposable);
            
            _storage.ObserveByType(StructureType.Court)
                .Subscribe(update => CasesCount.Value = update)
                .AddTo(_disposable);

            _storage.ObserveByType(StructureType.FireFighterStation)
                .Subscribe(update => FiresCount.Value = update)
                .AddTo(_disposable);

            _storage.ObserveByType(StructureType.PoliceStation)
                .Subscribe(update => ProtectionsCount.Value = update)
                .AddTo(_disposable);

            _storage.ObserveByType(StructureType.Hospital)
                .Subscribe(update => CuresCount.Value = update)
                .AddTo(_disposable);
            
            _storage.ObserveByType(StructureType.Archive)
                .Subscribe(update => ArchivesCount.Value = update)
                .AddTo(_disposable);
        }


        public void Dispose() {
            _disposable.Dispose();
            UpgradesTab.Dispose();
        }
  }
}
