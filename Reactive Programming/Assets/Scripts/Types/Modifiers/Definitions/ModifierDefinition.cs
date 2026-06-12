using System;
using Types.Buildings;
using Types.Economy.Modifiers;
using Types.Economy.Modifiers.Target;
using Types.Modifiers.Context;
using UnityEngine;

namespace Types.Modifiers {
    public abstract class ModifierDefinition : ScriptableObject {
        
        public ModifierTarget Target;
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