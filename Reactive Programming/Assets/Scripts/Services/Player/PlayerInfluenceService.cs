using System;
using R3;
using Types.Modifiers.Definitions;
using Types.Modifiers.Definitions.Buildings;
using UnityEngine;

namespace Services.Player {
    public class PlayerEffectService : IService, IStartable, IDisposable {
        private readonly SessionContext _context;
        private readonly BuildingUpgradeService _buildingUpgradeService;
        private readonly InvalidationService _invalidationService;
        
        private CompositeDisposable _disposable = new();
        
        public PlayerEffectService(SessionContext context, 
            BuildingUpgradeService buildingUpgradeService, 
            InvalidationService invalidationService) {
            _context = context;
            _buildingUpgradeService = buildingUpgradeService;
            _invalidationService = invalidationService;
        }


        public void StartService() {
            _buildingUpgradeService.OnBuildingUpgrade.Subscribe(OnBuildingUpgrade).AddTo(_disposable);
        }

        private void OnBuildingUpgrade(BuildingUpgrade upgrade) {
            var definition = upgrade.Building.Definition;
            var level = upgrade.Building.Level;

            var typeInfluence = _context.GetInfluenceInternalValue(definition.Type);
            var influenceWithoutBuilding = typeInfluence - definition.Influence * level;
            if (influenceWithoutBuilding <= 0) {
                Invalidate();
                _context.SetInfluence(definition.Type, upgrade.Building.Level * definition.Influence);
            }
            else {
                var buildingInfluence = upgrade.Building.Level * definition.Influence;
                _context.SetInfluence(definition.Type, influenceWithoutBuilding + buildingInfluence);
            }

            VerifyInfluenceForUpdate(upgrade.Building);
        }

        private void Invalidate() {
            _invalidationService.InvalidateAll();
        }

        private void VerifyInfluenceForUpdate(BuildingState state) {
            var type = state.Definition.Type;
            var internalValue = _context.GetInfluenceInternalValue(type);
            var externalValue = _context.GetInfluenceValue(type);
            if (externalValue == 0) {
                _context.UpdateInfluence(type);
                return;
            }
            var ratio = (float) Math.Log10(externalValue) / Math.Log10(internalValue);
            if (ratio < 0.95) {
                _context.UpdateInfluence(type);
                Invalidate();
            }
        }

        public void Dispose() {
            _disposable.Dispose();
        }
    }
}