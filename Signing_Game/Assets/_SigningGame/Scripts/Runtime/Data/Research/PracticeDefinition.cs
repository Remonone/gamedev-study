using System;
using Data.Modifiers;
using UnityEngine;
using Utils;

namespace Data.Research {
    public enum PracticeEffectKind {
        NumericModifiers = 0,
        InstantMoney = 1
    }

    [CreateAssetMenu(menuName = "Research/Practice", fileName = "Practice")]
    public sealed class PracticeDefinition : ScriptableObject {
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        public Sprite Icon;
        public string RarityId;
        [Range(0.01f, 1f)] public float SignatureThreshold = 0.5f;
        public PracticeEffectKind EffectKind;
        public ModifierDefinition[] Modifiers = Array.Empty<ModifierDefinition>();
        public Value InstantMoney = Value.Zero;
        [Min(0f)] public float FailedSignatureDurationSeconds;
    }
}
