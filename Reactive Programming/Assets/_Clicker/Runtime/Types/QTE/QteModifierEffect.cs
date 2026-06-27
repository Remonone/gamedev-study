using System;
using Types.Enums;
using UnityEngine;

namespace Types.QTE {
    [Serializable]
    public class QteModifierEffect {
        [Tooltip("QTE parameter affected by this effect.")]
        public QteModifierType Type = QteModifierType.SpawnIntervalSeconds;
        [Tooltip("Operation used when combining this effect with the base value.")]
        public ModifierOp Operation = ModifierOp.AddPercent;
        [Tooltip("Effect value. AddPercent uses 0.1 for +10%, Multiply uses the direct multiplier.")]
        public float Value;
        [Tooltip("Priority used when multiple Override effects target the same value.")]
        public int Priority;
    }
}
