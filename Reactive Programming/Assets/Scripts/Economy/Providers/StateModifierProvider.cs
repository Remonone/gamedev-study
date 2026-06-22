using System;
using System.Collections.Generic;
using Types.Modifiers.Definitions;
using Types.Modifiers.Definitions.Buildings;
using Types.Modifiers.Definitions.Context;
using Types.Modifiers.Definitions.Custom;

namespace Economy.Providers {
    public class StateModifierProvider : IModifierProvider {

        private IModifier _modifier;
        
        public StateModifierProvider() {
            _modifier = new StateModifierDefinition();
        }
        
        public void Collect(ISessionContext context, BuildingState building, List<StatModifier> modifiers) {
            foreach (var type in (GovernmentInteractionType[])Enum.GetValues(typeof(GovernmentInteractionType))) {
                var modifierContext = BuildContext(context, type);
                var modifier = _modifier.Resolve(null, modifierContext);
                if (modifier.HasValue) {
                    modifiers.Add(modifier.Value);
                }
            }
        }

        private IModifierContext BuildContext(ISessionContext context, GovernmentInteractionType type) {
            ModifierContext modifierContext = new ModifierContext();
            modifierContext.Add(new SessionCapability(context));
            modifierContext.Add(new TypeCapability(type));
            return modifierContext;
        }
    }
}