using System.Collections.Generic;
using R3;

namespace Types.Modifiers.Definitions {
    public class SessionContext : ISessionContext {
       
        private readonly int _seed;
        
        private Dictionary<GovernmentInteractionType, Influence> _governmentInfluence = new Dictionary<GovernmentInteractionType, Influence> {
            [GovernmentInteractionType.MayorOffice] = new(0),
            [GovernmentInteractionType.FireFighterStation] = new(0),
            [GovernmentInteractionType.PoliceStation] = new(0),
            [GovernmentInteractionType.Hospital] = new(0),
            [GovernmentInteractionType.Court] = new(0),
            [GovernmentInteractionType.Archive] = new(0),
        };
        
        public int Seed => _seed;

        public SessionContext() {
            _seed = UnityEngine.Random.Range(0, int.MaxValue);
        }
        
        public SessionContext(int mayorInfluence, int firefighterInfluence, int policeInfluence, int ambulanceInfluence, int courtInfluence, int archiveInfluence) {
            _governmentInfluence[GovernmentInteractionType.MayorOffice].ValueInternal = mayorInfluence;
            _governmentInfluence[GovernmentInteractionType.FireFighterStation].ValueInternal = firefighterInfluence;
            _governmentInfluence[GovernmentInteractionType.PoliceStation].ValueInternal = policeInfluence;
            _governmentInfluence[GovernmentInteractionType.Hospital].ValueInternal = ambulanceInfluence;
            _governmentInfluence[GovernmentInteractionType.Court].ValueInternal = courtInfluence;
            _governmentInfluence[GovernmentInteractionType.Archive].ValueInternal = archiveInfluence;
            _seed = UnityEngine.Random.Range(0, int.MaxValue);
        }
        
        public override string ToString() {
            return $"Mayor: {GetInfluenceInternalValue(GovernmentInteractionType.MayorOffice)}, Firefighter: {GetInfluenceInternalValue(GovernmentInteractionType.FireFighterStation)}, Police: {GetInfluenceInternalValue(GovernmentInteractionType.PoliceStation)}, Ambulance: {GetInfluenceInternalValue(GovernmentInteractionType.Hospital)}, Court: {GetInfluenceInternalValue(GovernmentInteractionType.Court)}, Archive: {GetInfluenceInternalValue(GovernmentInteractionType.Archive)}";
        }

        public void SetInfluence(GovernmentInteractionType type, int value) {
            _governmentInfluence[type].ValueInternal = value;
        }

        public int GetInfluenceInternalValue(GovernmentInteractionType type) {
            return _governmentInfluence[type].ValueInternal;
        }
        
        public int GetInfluenceValue(GovernmentInteractionType type) {
            return _governmentInfluence[type].ValueExternal;
        }

        public void UpdateInfluence(GovernmentInteractionType type) {
            var influence = _governmentInfluence[type];
            influence.ValueExternal = influence.ValueInternal;
        }

        private class Influence {
            public int ValueInternal;
            public int ValueExternal;
            
            public Influence(int value) {
                ValueInternal = value;
                ValueExternal = value;
            }
        }
    }
}