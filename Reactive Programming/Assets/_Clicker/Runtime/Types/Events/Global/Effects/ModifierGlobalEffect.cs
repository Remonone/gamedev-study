using System.Collections.Generic;
using Types.Buildings;
using Types.Modifiers;
using Types.Modifiers.Definitions;
using Types.Modifiers.Definitions.Context;

namespace Types.Events.Global.Effects {
    public class ModifierGlobalEffect : GlobalEffect {
        
        public ModifierDefinition[] Modifiers;

        public override void CollectModifiers(ISessionContext context, BuildingState building, List<StatModifier> output) {
            if (building == null || Modifiers == null || output == null) return;

            var modifierContext = new ModifierContext();
            modifierContext.Add(new SessionCapability(context));

            foreach (var definition in Modifiers) {
                if (definition == null || definition.Target == null || !definition.CanResolve(modifierContext)) {
                    continue;
                }

                var modifier = definition.Resolve(building, modifierContext);
                if (modifier.HasValue) {
                    output.Add(modifier.Value);
                }
            }
        }
    }
}
