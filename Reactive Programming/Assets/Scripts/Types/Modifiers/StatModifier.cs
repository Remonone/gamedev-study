using UnityEngine;

namespace Types.Enums {
    [System.Serializable]
    public struct StatModifier {
        [Tooltip("Stat affected by this modifier.")]
        public StatType Stat;
        [Tooltip("How Value changes the selected stat.")]
        public ModifierOp Operation;
        [Tooltip("Modifier amount. AddPercent uses 0.1 for +10%, Multiply uses the direct multiplier.")]
        public float Value;
        [Tooltip("Priority used when multiple Override modifiers target the same stat.")]
        public int Priority;
        [Tooltip("Stable id for identifying this modifier in debug output or future replacement logic.")]
        public string ModifierId;

    }
}
