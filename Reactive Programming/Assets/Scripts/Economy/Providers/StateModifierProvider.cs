using System;
using System.Collections.Generic;
using R3;
using Services;
using Types.Modifiers.Definitions;
using Types.Modifiers.Definitions.Buildings;

namespace Economy.Providers {
    public class StateModifierProvider : IModifierProvider, IDisposable {
        private readonly BuildingUpgradeService _buildingUpgradeService;
        private CompositeDisposable _disposable = new();
        
        public StateModifierProvider(BuildingUpgradeService buildingUpgradeService) {
            _buildingUpgradeService = buildingUpgradeService;
            _buildingUpgradeService.OnBuildingUpgrade.Subscribe(BuildingUpgraded).AddTo(_disposable);
        }
        
        
        public void Collect(ISessionContext context, BuildingState building, List<StatModifier> modifiers) {
            
        }
        
        private void BuildingUpgraded(BuildingUpgrade building) {
            
        }
        
        public void Dispose() {
            _disposable.Dispose();
        }
    }
}