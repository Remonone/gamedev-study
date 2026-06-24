using System.Collections.Generic;
using Types.Buildings;
using Types.Modifiers;

namespace Economy.Providers {
    public interface IModifierProvider {
        void Collect(ISessionContext context, BuildingState building, List<StatModifier> modifiers);
    }
}