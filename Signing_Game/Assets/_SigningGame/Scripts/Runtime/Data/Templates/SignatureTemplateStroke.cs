using System;
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

        public SignatureTemplateStroke(string id, IReadOnlyList<SignatureCorridorNode> nodes, bool required,
            float importance, float minimumCoverage, bool allowReverseDirection, float directionImportance,
            float length) {
            Id = id;
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            var snapshot = new SignatureCorridorNode[nodes.Count];
            for (int i = 0; i < snapshot.Length; i++) snapshot[i] = nodes[i];
            Nodes = Array.AsReadOnly(snapshot);
            Required = required;
            Importance = importance;
            MinimumCoverage = minimumCoverage;
            AllowReverseDirection = allowReverseDirection;
            DirectionImportance = directionImportance;
            Length = length;
        }
    }
}
