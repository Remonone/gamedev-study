using System.Collections.Generic;
using UnityEngine;

namespace Types.Events.Global {
    [CreateAssetMenu(fileName = "New Global Event", menuName = "Events/Global Event")]
    public class GlobalEvent : ScriptableObject {
        public string Name;
        public string Description;
        public Sprite Icon;
        public List<GlobalEffect> Effects;
    }
}