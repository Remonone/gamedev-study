using System;
using Types.Buildings;
using Types.Modifiers.Definitions.Context;
using Types.Modifiers.Target;
using UnityEngine;

namespace Types.Modifiers.Definitions {
    public abstract class ModifierDefinition : ScriptableObject, IModifier {
        
        [Tooltip("Buildings that can receive this modifier.")]
        public ModifierTarget Target;
        [Tooltip("Stat operation applied to matching buildings.")]
        public StatModifier Modifier;
        
        public abstract bool CanResolve(IModifierContext context);

        public StatModifier? Resolve(BuildingState state, IModifierContext context) {
            if (!CanResolve(context))
                throw new InvalidOperationException(
                    $"Cannot resolve context: {context} for {GetType().Name}");
            if (!Target.Matches(state)) return null;
            return ResolveInternal(state, context);
        }
        protected abstract StatModifier? ResolveInternal(BuildingState state, IModifierContext context);
    }
}
