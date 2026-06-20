namespace Types.Modifiers.Definitions.Upgrades {
    public class UpgradeNodeState {
        public enum State {Locked, Available, InProgress, Completed}

        public UpgradeNodeDefinition Definition;
        public int Level;
        public State CurrentState;

        public UpgradeNodeState(UpgradeNodeDefinition definition) {
            Definition = definition;
            Level = 0;
            CurrentState = State.Available;
        }

        public UpgradeNodeState(UpgradeNodeDefinition definition, int level, State currentState) {
            Definition = definition;
            Level = level;
            CurrentState = currentState;
        }

        public UpgradeNodeState(UpgradeNodeState other) {
            Definition = other.Definition;
            Level = other.Level;
            CurrentState = other.CurrentState;
        }
    }
}
