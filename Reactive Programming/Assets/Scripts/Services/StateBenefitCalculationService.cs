using System;
using Types.Enums;
using Types.Enums.Buildings;

namespace Services {
    public class StateBenefitCalculationService : IService {
	    
	    private readonly ISessionContext _context;
        
		public StateBenefitCalculationService(ISessionContext context) {
			_context = context;
        }
		
		public float CalculateBenefits(BuildingState state, float value) {
			value *= state.Cache.MultiplierCoefficient;
			CalculateCritChance(state, ref value);
			CalculateStability(ref value);
			return value;
		}

		private void CalculateStability(ref float value) {
			var stablility = CalculateStabilityValue();
			value *= stablility;
		}

		private float CalculateStabilityValue() {
			var capital = _context.MayorInfluence + _context.ArchiveInfluence + _context.AmbulanceInfluence +
			              _context.FirefighterInfluence + _context.CourtInfluence;
			if (capital == 0) return 1f;
			var capitalDecreasal = 1 / Math.Log10(capital);
			var policeEffect = Math.Log10(3*_context.PoliceInfluence);
			return (float)(capitalDecreasal * policeEffect);
		}

		private void CalculateCritChance(BuildingState state, ref float value) {
			var seed = _context.Seed;
			var random = new System.Random(seed);
			var chance = random.Next(0, 100);
			if (chance < state.Cache.CriticalChance) {
				value *= state.Cache.CriticalMultiplier;
			}
		}
    }
}