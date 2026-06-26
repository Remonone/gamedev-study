using Types.Enums;

namespace Types.Modifiers {
    public interface ISessionContext {
        public int Seed { get; }
        
        int GetInfluenceValue(GovernmentInteractionType type);
    }
}