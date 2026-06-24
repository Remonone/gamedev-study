using System;
using Services.Player;
using R3;
using Types;
using Types.Buildings;

namespace Services.Components.Instances {
    public class StructureClickService : IService, IDisposable, IStartable {
        
        private readonly Storage _storage;
        private readonly WorldCastService _worldCastService;
        private readonly UnlockService _unlockService;
        private readonly EconomyService _economyService;
        private readonly StateBenefitCalculationService _calculationService;
        
        private readonly CompositeDisposable _disposable = new();
        
        private readonly Subject<StructureInteraction> _structureInteraction = new();
        public Observable<StructureInteraction> StructureInteraction => _structureInteraction;
        
        
        public StructureClickService(Storage storage, WorldCastService worldCastService, UnlockService unlockService, EconomyService economyService, StateBenefitCalculationService calculationService) {
            _storage = storage;
            _worldCastService = worldCastService;
            _unlockService = unlockService;
            _economyService = economyService;
            _calculationService = calculationService;
        }
        
        private void HandleStructureInteraction(BuildingState state) {
            if (!_unlockService.IsItemUnlocked(state.Definition.Type.ToString())) return;
            var type = state.Definition.Type;
            var computedStats = _economyService.ComputeStatsForBuilding(state);
            var benefitedValue = computedStats.ClickIncome;
            
            _calculationService.CalculateBenefits(state, ref benefitedValue);
            _calculationService.CalculateCritChance(state, ref benefitedValue);
            _storage.AddMoney(type, benefitedValue);
            _structureInteraction.OnNext(new StructureInteraction { GovernmentInteraction = type, InteractionResult = benefitedValue });
        }

        public void Dispose() {
            _disposable.Dispose();
        }

        public void StartService() {
            _worldCastService.StructureClicked.Subscribe(HandleStructureInteraction).AddTo(_disposable);
        }
    }
}
