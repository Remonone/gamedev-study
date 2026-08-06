using System;
using Data.Modifiers;
using UnityEngine;
using Utils;

namespace Data.Bills {
    [CreateAssetMenu(menuName = "Bills/Reward", fileName = "Bill Reward")]
    public sealed class BillRewardDefinition : ScriptableObject {
        public string Id;
        public string Name;
        [TextArea] public string Description;
        public Sprite Icon;

        [Header("Generation")]
        public Value BaseCost = Value.One;
        [Min(0.0001f)] public double BaseRequiredProgress = 1d;
        [Min(0)] public int MinimumRequirementCount;
        [Min(0)] public int MaximumRequirementCount;
        public bool Repeatable;

        [Header("While active")]
        [Min(0f)] public double BaseActiveGenerationBonus;

        [Header("On completion")]
        public Value MoneyReward;
        public ModifierDefinition[] CompletionModifiers = Array.Empty<ModifierDefinition>();
    }
}
