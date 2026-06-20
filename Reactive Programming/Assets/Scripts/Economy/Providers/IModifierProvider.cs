using System.Collections.Generic;
using Types.Modifiers.Definitions.Buildings;
using Types.Modifiers.Definitions;

namespace Economy.Providers {
    public interface IModifierProvider {
        void Collect(ISessionContext context, BuildingState building, List<StatModifier> modifiers);
    }
}