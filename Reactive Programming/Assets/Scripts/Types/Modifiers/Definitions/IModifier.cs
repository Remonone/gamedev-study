using Types.Modifiers.Definitions.Buildings;
using Types.Modifiers.Definitions.Context;

namespace Types.Modifiers.Definitions {
    public interface IModifier {
        public bool CanResolve(IModifierContext context);
        public StatModifier? Resolve(BuildingState state, IModifierContext context);
    }
}