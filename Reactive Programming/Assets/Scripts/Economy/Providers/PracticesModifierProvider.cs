using System.Collections.Generic;
using Types.Buildings;
using Newtonsoft.Json.Linq;
using Save;
using Types;
using Types.Economy;
using Types.Economy.Modifiers;

namespace Economy.Providers {
    public class PracticesModifierProvider : IModifierProvider, ISaveable {

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

        public string SaveKey { get; }
        public int Priority { get; }
        public JToken Save() {
            return null;
        }

        public void Load(JToken data) {
            return;
        }
    }
}