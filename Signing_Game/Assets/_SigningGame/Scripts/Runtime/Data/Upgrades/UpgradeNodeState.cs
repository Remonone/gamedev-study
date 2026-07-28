using System;

namespace Data.Upgrades {
    public class UpgradeNodeState : IEquatable<UpgradeNodeState> {
        public enum State { Locked, Available, InProgress, Completed }

        public UpgradeNodeDefinition Definition;
        public int Level;
        public State CurrentState;

        public UpgradeNodeState(UpgradeNodeDefinition definition, int level, State currentState) {
            Definition = definition;
            Level = level;
            CurrentState = currentState;
        }
        
        public UpgradeNodeState(UpgradeNodeDefinition definition) : this(definition, 0, State.Locked) { }

        public UpgradeNodeState(UpgradeNodeState other) {
            Definition = other.Definition;
            Level = other.Level;
            CurrentState = other.CurrentState;
        }
        

        public bool Equals(UpgradeNodeState other) {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(Definition, other.Definition) && Level == other.Level && CurrentState == other.CurrentState;
        }

        public override bool Equals(object obj) {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((UpgradeNodeState)obj);
        }

        public override int GetHashCode() {
            return HashCode.Combine(Definition, Level, (int)CurrentState);
        }
    }
}