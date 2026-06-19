using Services.Player;
using R3;
using Services;
using Types.Enums;
using Types.Enums.Buildings;

namespace Services.Components.Instances {
    public class StructureClickService : IService {
        
        private readonly Storage _storage;
        private readonly WorldCastService _worldCastService;
        private readonly UnlockService _unlockService;
        private readonly EconomyService _economyService;
        private readonly StateBenefitCalculationService _calculationService;
        
        private readonly Subject<StructureInteraction> _structureInteraction = new();
        public Observable<StructureInteraction> StructureInteraction => _structureInteraction;
        
        
        public StructureClickService(Storage storage, WorldCastService worldCastService, UnlockService unlockService, EconomyService economyService, StateBenefitCalculationService calculationService) {
            _storage = storage;
            _worldCastService = worldCastService;
            _worldCastService.StructureClicked.Subscribe(HandleStructureInteraction);
            _unlockService = unlockService;
            _economyService = economyService;
            _calculationService = calculationService;
        }
        
        private void HandleStructureInteraction(BuildingState state) {
            if (!_unlockService.IsItemUnlocked(state.Definition.Type.ToString())) return;
            var type = state.Definition.Type;
            var computedStats = _economyService.ComputeStatsForBuilding(state);
            var benefitedValue = (long)_calculationService.CalculateBenefits(state, computedStats.ClickIncome);
            _storage.AddMoney(type, benefitedValue);
            _structureInteraction.OnNext(new StructureInteraction { GovernmentInteraction = type, InteractionResult = benefitedValue });
        }
    }
}