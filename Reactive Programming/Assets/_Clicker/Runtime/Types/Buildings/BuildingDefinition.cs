using Types.Enums;
using Types.Modifiers.Cost;
using Types.Modifiers.Cost.Formula;
using UnityEngine;

namespace Types.Buildings {
    [CreateAssetMenu(fileName = "Building", menuName = "Clicker/Building", order = 0)]
    public class BuildingDefinition : ScriptableObject {
        [Tooltip("Display name used in UI and as the building lookup key.")]
        public string Name;
        [SerializeField, TextArea(2, 4), Tooltip("Short UI description shown on the building card.")]
        public string Description;
        [SerializeField, Tooltip("Sprite shown as this building's icon in UI.")]
        public Sprite Icon;
        [SerializeField, Tooltip("Price formula evaluated with the current building level.")]
        public CostResolver Cost;
        [Tooltip("Resource category produced and spent by this building.")]
        public GovernmentInteractionType Type;
        [SerializeReference, Tooltip("Income granted by one manual click, evaluated with the current building level.")]
        public IFormula ClickIncome;
        [SerializeReference, Tooltip("Passive income value, evaluated with the current building level.")]
        public IFormula Income;
        [SerializeReference, Tooltip("Passive income ticks per second, evaluated with the current building level.")]
        public IFormula Frequency;
        [SerializeReference, Tooltip("Base stability value used by benefit calculations.")]
        public IFormula StabilityModifier;
        [SerializeReference, Tooltip("Multiplier applied to the stability modifier.")]
        public IFormula StabilityModifierMultiplier;
        [SerializeReference, Tooltip("Global multiplier coefficient applied to this building's output.")]
        public IFormula MultiplierCoefficient;
        [SerializeReference, Tooltip("Chance from 0 to 1 for a critical payout.")]
        public IFormula CriticalChance;
        [SerializeReference, Tooltip("Payout multiplier used when a critical payout triggers.")]
        public IFormula CriticalMultiplier;
        [SerializeField, Tooltip("Whether this building can be upgraded from the shop UI.")]
        public bool IsUpgradeable = true;
        [SerializeField, Tooltip("Amount of influence the building produces on the economy.")]
        public int Influence;
    }
}
