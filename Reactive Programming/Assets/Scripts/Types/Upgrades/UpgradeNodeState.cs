namespace Types.Upgrades {
    public class UpgradeNodeState {
        public enum State {Locked, Available, InProgress, Completed}
        
        public UpgradeNodeDefinition Definition;
        public int Level;
        public State CurrentState;
    }
}