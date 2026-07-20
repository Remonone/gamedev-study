using System;
using Contracts;
using Data.Rules;
using Data.Templates;
using Exceptions;

namespace Services {
    public class RuleResolver : IService, ISignatureRulesResolver {
        public void Dispose() {
            
        }

        public ResolvedSignatureRules Resolve(CompiledSignaturePreset preset, SignatureDifficultyRules difficulty,
            SignatureRuleModifiers modifiers) {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            if (difficulty == null) throw new ArgumentNullException(nameof(difficulty));
            ValidateProcessing(preset.Processing);
            if (difficulty.ScoreWeights == null) throw Error("Difficulty score weights are required.");
            ValidateFinite(difficulty.MinimumSimilarity, "Minimum similarity");
            ValidateFinite(difficulty.CorridorWidthMultiplier, "Corridor width multiplier");
            ValidateFinite(difficulty.CoverageRequirementMultiplier, "Coverage requirement multiplier");
            ValidateFinite(difficulty.AlignmentToleranceMultiplier, "Alignment tolerance multiplier");
            ValidateFinite(modifiers.CorridorWidthMultiplier, "Modifier corridor width multiplier");
            ValidateFinite(modifiers.MinimumSimilarityOffset, "Minimum similarity offset");
            ValidateFinite(modifiers.CoverageRequirementMultiplier, "Modifier coverage multiplier");
            ValidateFinite(modifiers.AlignmentToleranceMultiplier, "Modifier alignment multiplier");
            ValidateFinite(modifiers.DirectionContributionMultiplier, "Direction contribution multiplier");
            if (difficulty.MinimumSimilarity < 0f || difficulty.MinimumSimilarity > 1f)
                throw Error("Minimum similarity must be in [0,1].");
            float threshold = difficulty.MinimumSimilarity + modifiers.MinimumSimilarityOffset;
            if (threshold < 0f || threshold > 1f) throw Error("Effective minimum similarity must be in [0,1].");
            float width = difficulty.CorridorWidthMultiplier * modifiers.CorridorWidthMultiplier;
            float coverage = difficulty.CoverageRequirementMultiplier * modifiers.CoverageRequirementMultiplier;
            float alignmentMultiplier = difficulty.AlignmentToleranceMultiplier * modifiers.AlignmentToleranceMultiplier;
            if (!Finite(width) || width <= 0f) throw Error("Effective corridor width must be positive and finite.");
            if (!Finite(coverage) || coverage < 0f) throw Error("Effective coverage multiplier must be nonnegative and finite.");
            if (!Finite(alignmentMultiplier) || alignmentMultiplier < 0f) throw Error("Effective alignment multiplier must be nonnegative and finite.");
            if (modifiers.DirectionContributionMultiplier < 0f) throw Error("Direction contribution multiplier must be nonnegative.");

            SignatureScoreWeights source = difficulty.ScoreWeights;
            float fit = Weight(source.CorridorFit, "Corridor fit weight");
            float coverageWeight = Weight(source.Coverage, "Coverage weight");
            float direction = Weight(source.Direction, "Direction weight") * modifiers.DirectionContributionMultiplier;
            float structure = Weight(source.StrokeStructure, "Stroke structure weight");
            float total = fit + coverageWeight + direction + structure;
            if (!Finite(total) || total <= 0f) throw Error("Effective score weights must have a positive total.");

            SignatureAlignmentRules sourceAlignment = preset.Alignment;
            if (sourceAlignment == null) throw Error("Compiled alignment is required.");
            float minimumScale = 1f - (1f - sourceAlignment.MinimumScale) * alignmentMultiplier;
            float maximumScale = 1f + (sourceAlignment.MaximumScale - 1f) * alignmentMultiplier;
            var alignment = new SignatureAlignmentRules(sourceAlignment.MaximumTranslation * alignmentMultiplier,
                minimumScale, maximumScale, sourceAlignment.MaximumRotationDegrees * alignmentMultiplier);
            if (!Finite(alignment.MaximumTranslation) || alignment.MaximumTranslation < 0f ||
                !Finite(alignment.MinimumScale) || alignment.MinimumScale <= 0f ||
                !Finite(alignment.MaximumScale) || alignment.MaximumScale < alignment.MinimumScale ||
                !Finite(alignment.MaximumRotationDegrees) || alignment.MaximumRotationDegrees < 0f)
                throw Error("Resolved alignment values are invalid.");

            return new ResolvedSignatureRules(threshold, width, coverage, preset.Processing, alignment,
                new SignatureScoreWeights(fit / total, coverageWeight / total, direction / total, structure / total),
                preset.StrokeMatchMode);
        }

        private static float Weight(float value, string name) {
            ValidateFinite(value, name);
            if (value < 0f) throw Error(name + " must be nonnegative.");
            return value;
        }
        private static void ValidateProcessing(SignatureProcessingRules processing) {
            if (processing == null || !Finite(processing.MinimumInputPointDistance) || processing.MinimumInputPointDistance < 0f ||
                processing.MinimumUsablePointCountPerStroke < 2 || !Finite(processing.MinimumStrokeLength) ||
                processing.MinimumStrokeLength < 0f || processing.ResampledPointCountPerStroke < 2 ||
                processing.SmoothingPasses < 0 || processing.MaximumInputPointCount < processing.MinimumUsablePointCountPerStroke)
                throw Error("Compiled processing rules are invalid.");
        }
        private static void ValidateFinite(float value, string name) { if (!Finite(value)) throw Error(name + " must be finite."); }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static SignaturePresetConfigurationException Error(string message) => new(message);
    }
}
