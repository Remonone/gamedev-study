using System.Collections.Generic;
using Types.Buildings;
using Types.Economy;
using Types.Economy.Modifiers;

namespace Economy.Providers {
    public interface IModifierProvider {
        void Collect(SessionContext context, BuildingState building, List<StatModifier> modifiers);
    }
}