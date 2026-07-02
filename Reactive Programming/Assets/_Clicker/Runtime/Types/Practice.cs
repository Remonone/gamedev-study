using System.Collections.Generic;
using Types.Modifiers;
using Types.Modifiers.Definitions;
using Types.QTE;
using UnityEngine;

namespace Types {
    [CreateAssetMenu(fileName = "Practice", menuName = "Clicker/Practices/Practice", order = 0)]
    public class Practice : ScriptableObject {
        [SerializeField, Tooltip("Stable id used by save files. Must be unique across all practices.")]
        private string _id;
        [SerializeField, Tooltip("Name shown in UI.")]
        private string _displayName;
        [SerializeField, TextArea(2, 5), Tooltip("Description shown in UI.")]
        private string _description;
        [SerializeField, Tooltip("Icon shown in artifact grids and reward choices.")]
        private Sprite _icon;
        [SerializeField, Tooltip("Practice rarity/quality.")]
        private PracticeRarity _rarity;
        [SerializeField, Min(0f), Tooltip("Weighted chance inside this rarity. 0 excludes from random offers.")]
        private float _weight = 1f;

        [SerializeField, Tooltip("Regular stat modifiers resolved through the existing modifier system.")]
        private List<ModifierDefinition> _statModifiers = new();
        [SerializeField, Tooltip("Modifiers whose value is calculated from influence.")]
        private List<InfluencePracticeEffect> _influenceEffects = new();
        [SerializeField, Tooltip("Modifiers applied to research calculations.")]
        private List<ResearchPracticeEffect> _researchEffects = new();
        [SerializeReference, Tooltip("QTE improvements applied by this practice.")]
        private List<QteModifierEffect> _qteImprovements = new();

        public string Id => string.IsNullOrWhiteSpace(_id) ? name : _id;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;
        public string Description => _description;
        public Sprite Icon => _icon;
        public PracticeRarity Rarity => _rarity;
        public float Weight => Mathf.Max(0f, _weight);
        public IReadOnlyList<ModifierDefinition> StatModifiers => _statModifiers;
        public IReadOnlyList<InfluencePracticeEffect> InfluenceEffects => _influenceEffects;
        public IReadOnlyList<ResearchPracticeEffect> ResearchEffects => _researchEffects;
        public IReadOnlyList<QteModifierEffect> QteImprovements => _qteImprovements ??= new List<QteModifierEffect>();
    }

    public enum PracticeRarity {
        Common,
        Good,
        Unusual,
        Advanced,
        Brilliant
    }

    public enum PracticeResearchModifierType {
        PointsPerSecondMultiplier,
        RequiredPointsMultiplier,
        FlatPointsPerSecond
    }

    [System.Serializable]
    public class InfluencePracticeEffect {
        [Tooltip("Buildings that can receive this modifier.")]
        public Types.Modifiers.Target.ModifierTarget Target;
        [Tooltip("Influence source used in the formula. Ignored when UseAllInfluence is enabled.")]
        public Types.Enums.GovernmentInteractionType SourceInfluence;
        [Tooltip("Use sum of all influence values instead of one source.")]
        public bool UseAllInfluence;
        [Tooltip("Stat modified on matching buildings.")]
        public Types.Enums.StatType Stat;
        [Tooltip("Operation applied to matching buildings.")]
        public Types.Enums.ModifierOp Operation;
        [Tooltip("Base modifier value before influence conversion.")]
        public float BaseValue;
        [Tooltip("Additional value per influence point.")]
        public float ValuePerInfluence = 0.01f;
        [Tooltip("Priority for Override modifiers.")]
        public int Priority;
        [Tooltip("Stable debug id for this generated modifier.")]
        public string ModifierId;
    }

    [System.Serializable]
    public class ResearchPracticeEffect {
        [Tooltip("Research parameter affected by this practice.")]
        public PracticeResearchModifierType Type;
        [Tooltip("Operation used when combining this research effect.")]
        public Types.Enums.ModifierOp Operation = Types.Enums.ModifierOp.AddPercent;
        [Tooltip("Value of the effect. AddPercent uses 0.1 for +10%, Multiply uses direct multiplier.")]
        public float Value;
    }
}
