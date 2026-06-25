using System.Collections.Generic;
using Services.Events;
using Types.Buildings;
using Types.Events.Global;
using Types.Modifiers;

namespace Economy.Providers {
    public sealed class GlobalEventModifierProvider : IModifierProvider {
        private readonly GlobalEventService _globalEventService;

        public GlobalEventModifierProvider(GlobalEventService globalEventService) {
            _globalEventService = globalEventService;
        }

        public void Collect(ISessionContext context, BuildingState building, List<StatModifier> modifiers) {
            var activeEvent = _globalEventService.ActiveEvent;
            if (activeEvent?.Effects == null) return;

            foreach (GlobalEffect effect in activeEvent.Effects) {
                effect?.CollectModifiers(context, building, modifiers);
            }
        }
    }
}
