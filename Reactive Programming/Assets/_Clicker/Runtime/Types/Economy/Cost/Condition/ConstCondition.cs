using System;
using UnityEngine;

namespace Types.Modifiers.Cost.Condition {
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp")]
    public class ConstCondition : ILevelCondition {
        [SerializeField, Tooltip("Fixed result returned by this condition for every level.")]
        private bool _constant;
        public bool IsMet(int level) => _constant;
    }
}
