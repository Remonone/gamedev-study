using System;
using Types.Modifiers.Definitions;
using Types.Modifiers.Definitions.Buildings;
using Types.Modifiers.Definitions.Values;

namespace Services {
    public class StateBenefitCalculationService : IService {
	    
	    private readonly ISessionContext _context;
	    private readonly Random _random;
	    
		public StateBenefitCalculationService(ISessionContext context) {
			_context = context;
			_random = new Random(_context.Seed);
		}
		
		public void CalculateBenefits(BuildingState state, ref Value value) {
			var stability = CalculateStabilityValue();
			var multiplier = Math.Max(1f, state.Cache.MultiplierCoefficient * stability);
			value *= multiplier;
		}


		private float CalculateStabilityValue() {
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
			return (float)Math.Max(0, Math.Min(1f, capitalDecreasal * policeEffect));
		}

		public void CalculateCritChance(BuildingState state, ref Value value) {
			if (_random.NextDouble() < state.Cache.CriticalChance) {
				var multiplier = Math.Max(1f, state.Cache.CriticalMultiplier * CalculateStabilityValue());
				value *= multiplier;
			}
		}
    }
}
