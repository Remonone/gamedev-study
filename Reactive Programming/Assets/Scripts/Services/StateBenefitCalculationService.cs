using System;
using Types.Enums;
using Types.Enums.Buildings;
using Types.Enums.Values;

namespace Services {
    public class StateBenefitCalculationService : IService {
	    
	    private readonly ISessionContext _context;
	    private readonly Random _random;
	    
		public StateBenefitCalculationService(ISessionContext context) {
			_context = context;
			_random = new Random(_context.Seed);
		}
		
		public void CalculateBenefits(BuildingState state, ref Value value) {
			value *= state.Cache.MultiplierCoefficient;
			
			CalculateStability(ref value);
		}

		private void CalculateStability(ref Value value) {
			var stablility = CalculateStabilityValue();
			value *= stablility;
		}

		private float CalculateStabilityValue() {
			var capital = _context.MayorInfluence + _context.ArchiveInfluence + _context.AmbulanceInfluence +
			              _context.FirefighterInfluence + _context.CourtInfluence;
			if (capital < 1) return 1f;
			var capitalDecreasal = 1 / Math.Log10(capital);
			if (_context.PoliceInfluence < 1) {
				return (float)capitalDecreasal;
			}
			var policeEffect = Math.Log10(3*_context.PoliceInfluence);
			return (float)(capitalDecreasal * policeEffect);
		}

		public void CalculateCritChance(BuildingState state, ref Value value) {
			if (_random.NextDouble() < state.Cache.CriticalChance) {
				value *= state.Cache.CriticalMultiplier;
			}
		}
    }
}
