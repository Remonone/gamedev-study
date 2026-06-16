using System.Collections.Generic;
using Types.Enums.Buildings;
using Types.Enums;

namespace Economy.Providers {
    public class PracticesModifierProvider : IModifierProvider {

        private List<Practice> _practices;
        
        public PracticesModifierProvider(List<Practice> practices) {
            _practices = practices;
        }

        public void RegisterPractice(Practice practice) {
            _practices.Add(practice);
        }
        
        public void Collect(SessionContext context, BuildingState building, List<StatModifier> modifiers) {
            foreach (var practice in _practices) {
            }
        }
    }
}