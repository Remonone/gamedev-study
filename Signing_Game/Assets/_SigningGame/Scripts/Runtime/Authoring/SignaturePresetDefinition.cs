using System.Collections.Generic;
using Data.Enums;
using UnityEngine;

namespace Authoring {
    [CreateAssetMenu(
        fileName = "SignaturePreset",
        menuName = "Game/Signatures/Signature Preset")]
    public sealed class SignaturePresetDefinition : ScriptableObject {
        [SerializeField] private string _id;

        [SerializeField, Min(1)] private int _version = 1;

        [SerializeField] private SignatureStrokeMatchMode _strokeMatchMode =
            SignatureStrokeMatchMode.Ordered;

        [SerializeField] private SignatureAlignmentDefinition _alignment = new();

        [SerializeField] private List<SignatureTemplateVariantDefinition> _variants = new();

        public string Id => _id;
        public int Version => _version;
        public SignatureStrokeMatchMode StrokeMatchMode => _strokeMatchMode;
        public SignatureAlignmentDefinition Alignment => _alignment;

        public IReadOnlyList<SignatureTemplateVariantDefinition> Variants =>
            _variants;
    }
}