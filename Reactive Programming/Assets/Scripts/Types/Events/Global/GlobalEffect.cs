using System.Collections.Generic;
using Types.Buildings;
using Types.Modifiers;
using UnityEngine;

namespace Types.Events.Global {
    public abstract class GlobalEffect : ScriptableObject {
        public virtual void OnStarted() { }
        public virtual void OnEnded() { }

        public virtual void CollectModifiers(ISessionContext context, BuildingState building, List<StatModifier> output) { }
    }
}
