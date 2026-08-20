using System;
using System.Collections.Generic;
using Authoring;
using Contracts;
using Data.Enums;
using Data.Rules;
using Data.Templates;
using Exceptions;
using UnityEngine;

namespace Services {
    
    public sealed class SignaturePresetCompiler : IService, ISignaturePresetCompiler {
        private const float GeometryEpsilon = 0.000001f;

        public CompiledSignaturePreset Compile(SignaturePresetDefinition preset) {
            if (preset == null) throw Error("Preset is required.");
            if (string.IsNullOrWhiteSpace(preset.Id)) throw Error("Preset Id is required.");
            if (preset.ProcessingProfile == null) throw Error("Preset processing profile is required.");
            if (preset.GuidanceFullDisplayAfterSignatures < 0 || preset.GuidanceFadeOutSignatureCount < 0)
                throw Error("Signature guidance counts must be non-negative.");
            SignatureProcessingRules processing = preset.ProcessingProfile.ToRules();
            if (preset.StrokeMatchMode != SignatureStrokeMatchMode.Ordered)
                throw Error("Only Ordered stroke matching is supported; BestAssignment is not supported.");

            SignatureAlignmentDefinition alignment = preset.Alignment;
            if (alignment == null) throw Error("Alignment settings are required.");
            ValidateAlignment(alignment);
            if (preset.Variants == null || preset.Variants.Count == 0)
                throw Error("At least one signature variant is required.");

            var variantIds = new HashSet<string>(StringComparer.Ordinal);
            var variants = new List<SignatureTemplateVariant>(preset.Variants.Count);
            foreach (SignatureVariantDefinition variant in preset.Variants) {
                if (variant == null) throw Error("Preset contains a null variant.");
                if (string.IsNullOrWhiteSpace(variant.Id) || !variantIds.Add(variant.Id))
                    throw Error("Every variant must have a unique, nonempty Id.");
                if (variant.Strokes == null || variant.Strokes.Count == 0)
                    throw Error($"Variant '{variant.Id}' must contain at least one stroke.");

                var strokeIds = new HashSet<string>(StringComparer.Ordinal);
                var strokes = new List<SignatureTemplateStroke>(variant.Strokes.Count);
                float totalStrokeImportance = 0f;
                foreach (SignatureTemplateStrokeDefinition stroke in variant.Strokes) {
                    if (stroke == null) throw Error($"Variant '{variant.Id}' contains a null stroke.");
                    if (string.IsNullOrWhiteSpace(stroke.Id) || !strokeIds.Add(stroke.Id))
                        throw Error($"Every stroke in variant '{variant.Id}' must have a unique, nonempty Id.");
                    ValidateStroke(stroke, processing.ResampledPointCountPerStroke, variant.Id);
                    strokes.Add(CompileStroke(stroke));
                    totalStrokeImportance += stroke.Importance;
                }
                if (!Finite(totalStrokeImportance) || totalStrokeImportance <= 0f)
                    throw Error($"Variant '{variant.Id}' must have positive total stroke importance.");
                variants.Add(new SignatureTemplateVariant(variant.Id, strokes));
            }

            return new CompiledSignaturePreset(preset.Id, preset.ProcessingProfile.Id, processing,
                new SignatureAlignmentRules(alignment.MaximumTranslation, alignment.MinimumScale,
                    alignment.MaximumScale, alignment.MaximumRotationDegrees), preset.StrokeMatchMode,
                preset.GuidanceFullDisplayAfterSignatures, preset.GuidanceFadeOutSignatureCount, variants);
        }

        private static void ValidateStroke(SignatureTemplateStrokeDefinition stroke, int expectedCount,
            string variantId) {
            if (!Finite(stroke.Importance) || stroke.Importance < 0f)
                throw Error($"Stroke '{stroke.Id}' in variant '{variantId}' has invalid importance.");
            if (!Finite(stroke.MinimumCoverage) || stroke.MinimumCoverage < 0f || stroke.MinimumCoverage > 1f)
                throw Error($"Stroke '{stroke.Id}' has invalid minimum coverage.");
            if (!Finite(stroke.DirectionImportance) || stroke.DirectionImportance < 0f || stroke.DirectionImportance > 1f)
                throw Error($"Stroke '{stroke.Id}' has invalid direction importance.");
            if (stroke.Nodes == null || stroke.Nodes.Count < 2)
                throw Error($"Stroke '{stroke.Id}' must contain at least two nodes.");
            if (stroke.Nodes.Count != expectedCount)
                throw Error($"Stroke '{stroke.Id}' must contain exactly {expectedCount} nodes for its processing profile.");
            float totalNodeImportance = 0f;
            foreach (SignatureCorridorNodeDefinition node in stroke.Nodes) {
                if (node == null) throw Error($"Stroke '{stroke.Id}' contains a null node.");
                if (!Finite(node.Position.x) || !Finite(node.Position.y)) throw Error($"Stroke '{stroke.Id}' has a non-finite node position.");
                if (!Finite(node.Radius) || node.Radius <= 0f) throw Error($"Stroke '{stroke.Id}' has a non-positive or non-finite radius.");
                if (!Finite(node.Importance) || node.Importance < 0f) throw Error($"Stroke '{stroke.Id}' has invalid node importance.");
                totalNodeImportance += node.Importance;
            }
            if (!Finite(totalNodeImportance) || totalNodeImportance <= 0f)
                throw Error($"Stroke '{stroke.Id}' must have positive total node importance.");
        }

        private static SignatureTemplateStroke CompileStroke(SignatureTemplateStrokeDefinition source) {
            int count = source.Nodes.Count;
            var distances = new float[count];
            for (int i = 1; i < count; i++)
                distances[i] = distances[i - 1] + Vector2.Distance(source.Nodes[i - 1].Position, source.Nodes[i].Position);
            float length = distances[count - 1];
            if (!Finite(length) || length <= GeometryEpsilon)
                throw Error($"Stroke '{source.Id}' must have positive geometric length.");
            var nodes = new List<SignatureCorridorNode>(count);
            for (int i = 0; i < count; i++) {
                Vector2 delta = i == 0 ? source.Nodes[1].Position - source.Nodes[0].Position
                    : i == count - 1 ? source.Nodes[count - 1].Position - source.Nodes[count - 2].Position
                    : source.Nodes[i + 1].Position - source.Nodes[i - 1].Position;
                Vector2 direction = delta.sqrMagnitude <= GeometryEpsilon * GeometryEpsilon ? Vector2.zero : delta.normalized;
                SignatureCorridorNodeDefinition node = source.Nodes[i];
                nodes.Add(new SignatureCorridorNode(node.Position, node.Radius, node.Importance,
                    distances[i] / length, direction));
            }
            return new SignatureTemplateStroke(source.Id, nodes, source.Required, source.Importance,
                source.MinimumCoverage, source.AllowReverseDirection, source.DirectionImportance, length);
        }

        private static void ValidateAlignment(SignatureAlignmentDefinition alignment) {
            if (!Finite(alignment.MaximumTranslation) || alignment.MaximumTranslation < 0f ||
                !Finite(alignment.MinimumScale) || alignment.MinimumScale <= 0f ||
                !Finite(alignment.MaximumScale) || alignment.MaximumScale < alignment.MinimumScale ||
                !Finite(alignment.MaximumRotationDegrees) || alignment.MaximumRotationDegrees < 0f)
                throw Error("Alignment values must be finite, positive where required, and ordered.");
        }

        private static SignaturePresetConfigurationException Error(string message) => new(message);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public void Dispose() { }
    }
}
