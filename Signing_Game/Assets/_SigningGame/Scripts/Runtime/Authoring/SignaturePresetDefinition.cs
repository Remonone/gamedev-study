using System.Collections.Generic;
using Data.Enums;
using UnityEngine;

namespace Authoring {
    [CreateAssetMenu(
        fileName = "SignaturePreset",
        menuName = "Game/Signatures/Signature Preset")]
    public sealed class SignaturePresetDefinition : ScriptableObject {
        [SerializeField] private string _id;


        [SerializeField] private SignatureStrokeMatchMode _strokeMatchMode =
            SignatureStrokeMatchMode.Ordered;

        [SerializeField] private SignatureDifficultyProfileDefinition _baseDifficultyProfile;
        [SerializeField] private SignatureAlignmentDefinition _alignment = new();
        [SerializeField] private SignatureProcessingProfileDefinition _processingProfile;
        [SerializeField] private List<SignatureVariantDefinition> _variants = new();

        public string Id => _id;
        public SignatureDifficultyProfileDefinition BaseDifficultyProfile => _baseDifficultyProfile;
        public SignatureStrokeMatchMode StrokeMatchMode => _strokeMatchMode;
        public SignatureAlignmentDefinition Alignment => _alignment;
        public SignatureProcessingProfileDefinition ProcessingProfile => _processingProfile;
        public IReadOnlyList<SignatureVariantDefinition> Variants => _variants;
    }
}
