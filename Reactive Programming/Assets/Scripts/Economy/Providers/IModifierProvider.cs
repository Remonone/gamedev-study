using System.Collections.Generic;
using Bases.Buildings;
using Types.Economy;

namespace Economy.Providers {
    public interface IModifierProvider {
        void Collect(SessionContext context, BuildingState building, List<StatModifier> modifiers);
    }
}