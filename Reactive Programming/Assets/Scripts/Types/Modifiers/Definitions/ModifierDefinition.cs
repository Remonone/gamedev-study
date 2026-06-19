using System;
using Types.Enums.Buildings;
using Types.Enums.Context;
using Types.Enums;
using Types.Enums.Target;
using UnityEngine;

namespace Types.Enums {
    public abstract class ModifierDefinition : ScriptableObject {
        
        [Tooltip("Buildings that can receive this modifier.")]
        public ModifierTarget Target;
        [Tooltip("Stat operation applied to matching buildings.")]
        public StatModifier Modifier;
        
        protected abstract bool CanResolve(IModifierContext context);

        public StatModifier? Resolve(BuildingState state, IModifierContext context) {
            if (!CanResolve(context))
                throw new InvalidOperationException(
                    $"Cannot resolve context: {context.ToString()} for {GetType().Name}");
            return ResolveInternal(state, context);
        }
        protected abstract StatModifier? ResolveInternal(BuildingState state, IModifierContext context);
    }
}
