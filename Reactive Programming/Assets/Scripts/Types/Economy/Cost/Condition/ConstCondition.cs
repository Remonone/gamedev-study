using System;
using UnityEngine;

namespace Types.Economy.Cost.Condition {
    [Serializable]
    public class ConstCondition : ILevelCondition {
        [SerializeField] private bool _constant;
        public bool IsMet(int level) => _constant;
    }
}