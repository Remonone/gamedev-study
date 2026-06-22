using System.Collections.Generic;
using R3;

namespace Types.Modifiers.Definitions {
    public interface ISessionContext {
        public int Seed { get; }
        
        int GetInfluenceValue(GovernmentInteractionType type);
    }
}