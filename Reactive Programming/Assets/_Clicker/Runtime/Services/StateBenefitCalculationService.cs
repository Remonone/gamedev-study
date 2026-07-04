using System;
using Types.Modifiers;
using Types.Buildings;
using Types.Enums;
using Types.Values;

namespace Services {
    public class StateBenefitCalculationService : IService {
	    
	    private readonly ISessionContext _context;
	    private BuildingUpgradeService _buildingUpgradeService;
	    private readonly Random _random;
	    
	    private double _lastStabilityUpdate = 0;

	    private float _stabilityCache;
	    
		public StateBenefitCalculationService(ISessionContext context) {
			_context = context;
			_random = new Random(_context.Seed);
		}
		
		public void CalculateBenefits(BuildingState state, ref Value value) {
			var multiplier = state.Cache.MultiplierCoefficient * state.Cache.StabilityModifier;
			value *= multiplier;
		}


		public float CalculateStabilityValue() {
			if(_lastStabilityUpdate != 0 && Math.Abs(_lastStabilityUpdate - _context.LastInfluenceUpdate) < 0.001d)
				return _stabilityCache;
			_lastStabilityUpdate = _context.LastInfluenceUpdate;
			var capital = _context.GetInfluenceValue(GovernmentInteractionType.MayorOffice) 
			              + _context.GetInfluenceValue(GovernmentInteractionType.Archive) + _context.GetInfluenceValue(GovernmentInteractionType.Hospital) +
			              _context.GetInfluenceValue(GovernmentInteractionType.FireFighterStation) + _context.GetInfluenceValue(GovernmentInteractionType.Court);
			if (capital <= 1) return 1f;
			var capitalDecreasal = 1 / Math.Log10(capital);
			var police = _context.GetInfluenceValue(GovernmentInteractionType.PoliceStation);
			var court = _context.GetInfluenceValue(GovernmentInteractionType.Court);
			if (police <= 1) {
				return (float)Math.Max(0, Math.Min(1f, capitalDecreasal));
			}
			var policeEffect = Math.Log10(court+police);
			_stabilityCache = (float)Math.Max(0, Math.Min(1f, capitalDecreasal * policeEffect));
			return _stabilityCache;
		}

		public void CalculateCritChance(BuildingState state, ref Value value) {
			if (_random.NextDouble() < state.Cache.CriticalChance) {
				var multiplier = Math.Max(1f, state.Cache.CriticalMultiplier * state.Cache.StabilityModifier);
				value *= multiplier;
			}
		}
    }
}
