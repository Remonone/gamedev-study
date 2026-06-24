using System.Collections.Generic;
using Types;
using Types.Buildings;
using Types.Modifiers;

namespace Economy.Providers {
    public class PracticesModifierProvider : IModifierProvider {

        private List<Practice> _practices;
        
        public PracticesModifierProvider(List<Practice> practices) {
            _practices = practices;
        }

        public void RegisterPractice(Practice practice) {
            _practices.Add(practice);
        }
        
        public void Collect(ISessionContext context, BuildingState building, List<StatModifier> modifiers) {
            foreach (var practice in _practices) {
            }
        }
    }
}