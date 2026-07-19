using System;
using System.Collections.Generic;
using UnityEngine;

namespace Authoring {
    [Serializable]
    public sealed class SignatureVariantDefinition {
        [SerializeField] private string _id;
        [SerializeField] private List<SignatureTemplateStrokeDefinition> _strokes = new();
        public string Id => _id;
        public IReadOnlyList<SignatureTemplateStrokeDefinition> Strokes => _strokes;
    }
}
