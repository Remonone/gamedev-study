using System.Collections.Generic;

namespace Data.Templates {
    public class SignatureTemplateStroke {
        public string Id { get; }
        public IReadOnlyList<SignatureCorridorNode> Nodes { get; }

        public bool Required { get; }
        public float Importance { get; }
        public float MinimumCoverage { get; }

        public bool AllowReverseDirection { get; }
        public float DirectionImportance { get; }

        public float Length { get; }
    }
}