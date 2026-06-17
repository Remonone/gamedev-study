using System.Collections.Generic;
using Types.Enums.Buildings;
using Types.Enums;

namespace Economy.Providers {
    public interface IModifierProvider {
        void Collect(ISessionContext context, BuildingState building, List<StatModifier> modifiers);
    }
}