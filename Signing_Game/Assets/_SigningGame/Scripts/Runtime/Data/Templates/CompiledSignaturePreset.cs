using System.Collections.Generic;
using Data.Enums;
using Data.Rules;

namespace Data.Templates {
    public class CompiledSignaturePreset {
        public string Id { get; }
        public int Version { get; }

        public SignatureStrokeMatchMode StrokeMatchMode { get; }

        public SignatureAlignmentRules Alignment { get; }

        public IReadOnlyList<SignatureTemplateVariant> Variants { get; }
    }
}