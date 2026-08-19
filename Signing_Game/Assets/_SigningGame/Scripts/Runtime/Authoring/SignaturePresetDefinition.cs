using System.Collections.Generic;
using Data.Enums;
using Utils;
using UnityEngine;

namespace Authoring {
    [CreateAssetMenu(
        fileName = "SignaturePreset",
        menuName = "Game/Signatures/Signature Preset")]
    public sealed class SignaturePresetDefinition : ScriptableObject {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private List<string> _tags = new();
        [SerializeField] private SignatureCategory _category = SignatureCategory.Simple;
        [SerializeField] private Value _baseIncome = Value.One;
        [SerializeField] private SignatureStrokeMatchMode _strokeMatchMode =
            SignatureStrokeMatchMode.Ordered;

        [SerializeField] private SignatureDifficultyProfileDefinition _baseDifficultyProfile;
        [SerializeField] private SignatureAlignmentDefinition _alignment = new();
        [SerializeField] private SignatureProcessingProfileDefinition _processingProfile;
        [SerializeField] private List<SignatureVariantDefinition> _variants = new();

        public string Id => _id;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;
        public IReadOnlyList<string> Tags => _tags;
        public SignatureCategory Category => _category;
        public Value BaseIncome => _baseIncome;
        public SignatureDifficultyProfileDefinition BaseDifficultyProfile => _baseDifficultyProfile;
        public SignatureStrokeMatchMode StrokeMatchMode => _strokeMatchMode;
        public SignatureAlignmentDefinition Alignment => _alignment;
        public SignatureProcessingProfileDefinition ProcessingProfile => _processingProfile;
        public IReadOnlyList<SignatureVariantDefinition> Variants => _variants;

        public bool HasTag(string tag) {
            if (string.IsNullOrWhiteSpace(tag)) return false;
            for (int index = 0; index < _tags.Count; index++) {
                if (string.Equals(_tags[index], tag, System.StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
