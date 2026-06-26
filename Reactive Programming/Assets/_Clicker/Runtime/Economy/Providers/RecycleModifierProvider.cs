using System.Collections.Generic;
using Services;
using Types.Buildings;
using Types.Modifiers;

namespace Economy.Providers {
    public class RecycleModifierProvider : IModifierProvider {
        
        private readonly IReadOnlyList<PracticeRecycleModifier> _recycleModifiers;

        public RecycleModifierProvider(RecycleService recycleService) {
            _recycleModifiers = recycleService.RecycleModifiers;
        }
        
        public void Collect(ISessionContext context, BuildingState building, List<StatModifier> modifiers) {
            foreach (var modifier in _recycleModifiers) {
                if (!modifier.AppliesToAllClicks && building.Definition.Name != modifier.TargetBuildingName) {
                    continue;
                }

                modifiers.Add(new StatModifier {
                    Stat = modifier.Stat,
                    Operation = modifier.Operation,
                    Value = modifier.Value,
                    Priority = modifier.Priority,
                    ModifierId = modifier.ModifierId
                });
            }
        }
    }
}