using Types.Enums;

namespace Types.Modifiers {
    public interface ISessionContext {
        public int Seed { get; }
        
        public double LastInfluenceUpdate { get; }
        
        int GetInfluenceValue(GovernmentInteractionType type);
    }
}