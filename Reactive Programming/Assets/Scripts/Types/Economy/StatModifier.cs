namespace Types.Economy {
    [System.Serializable]
    public struct StatModifier {
        public StatType Stat;
        public ModifierOp Operation;
        public float Value;
        public int Priority;
        public string ModifierId;
        public ModifierTarget Target;
    }
}
