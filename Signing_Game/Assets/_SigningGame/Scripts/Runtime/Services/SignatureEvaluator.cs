using System;
using System.Collections.Generic;
using Authoring;
using Contracts;
using Data.Enums;
using Data.Input;
using Data.Processed;
using Data.Requests;
using Data.Results;
using Data.Rules;
using Data.Templates;
using Services.Locator;
using UnityEngine;

namespace Services {
    public sealed class SignatureEvaluator : IService, ISignatureEvaluator, IInitialize {
        private static readonly SignatureScoreBreakdown EmptyBreakdown = new(0f, 0f, 0f, 0f, 0f);
        private static readonly IReadOnlyList<SignatureStrokeMatchResult> EmptyStrokeResults =
            Array.AsReadOnly(Array.Empty<SignatureStrokeMatchResult>());
        private ISignaturePreprocessor _preprocessor;
        private ISignaturePresetRepository _repository;
        private ISignatureRulesResolver _resolver;
        private ISignatureMatcher _matcher;
        private bool _initialized;

        Awaitable IInitialize.InitializeAsync(IServiceScope scope) {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            ISignaturePreprocessor preprocessor = scope.Get<ISignaturePreprocessor>();
            ISignaturePresetRepository repository = scope.Get<ISignaturePresetRepository>();
            ISignatureRulesResolver resolver = scope.Get<ISignatureRulesResolver>();
            ISignatureMatcher matcher = scope.Get<ISignatureMatcher>();

            _preprocessor = preprocessor;
            _repository = repository;
            _resolver = resolver;
            _matcher = matcher;
            _initialized = true;

            var source = new AwaitableCompletionSource();
            Awaitable awaitable = source.Awaitable;
            source.SetResult();
            return awaitable;
        }

        public SignatureEvaluationResult Evaluate(SignatureEvaluationRequest request) {
            EnsureInitialized();
            if (request == null) throw new ArgumentNullException(nameof(request));
            return Evaluate(request.Attempt, request.Preset, request.Difficulty, request.Modifiers);
        }

        public SignatureEvaluationResult Evaluate(SignatureAttempt attempt, SignaturePresetDefinition preset,
            SignatureDifficultyRules difficulty, SignatureRuleModifiers modifiers) {
            EnsureInitialized();
            if (attempt == null) throw new ArgumentNullException(nameof(attempt));
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            if (difficulty == null) throw new ArgumentNullException(nameof(difficulty));
            if (attempt.Strokes == null || attempt.Strokes.Count == 0)
                return Invalid(SignatureFailureReason.EmptyAttempt);

            CompiledSignaturePreset compiled = _repository.GetOrCompile(preset);
            if (HasOversizedStroke(attempt, compiled.Processing))
                return Invalid(SignatureFailureReason.TooManyPoints);
            ProcessedSignature processed = _preprocessor.Process(attempt, compiled.Processing);
            if (processed == null) return Invalid(DetermineFailure(attempt, compiled.Processing));
            ResolvedSignatureRules rules = _resolver.Resolve(compiled, difficulty, modifiers);

            SignatureVariantMatchResult best = null;
            foreach (SignatureTemplateVariant variant in compiled.Variants) {
                SignatureVariantMatchResult candidate = _matcher.Match(processed, variant, rules);
                if (best == null || candidate.Similarity > best.Similarity) best = candidate;
            }
            bool accepted = best.Similarity >= rules.MinimumSimilarity;
            return new SignatureEvaluationResult(accepted ? SignatureEvaluationStatus.Accepted : SignatureEvaluationStatus.Rejected,
                accepted ? SignatureFailureReason.None : SignatureFailureReason.BelowSimilarityThreshold,
                Mathf.Clamp01(best.Similarity), rules.MinimumSimilarity, best.Breakdown);
        }

        private static bool HasOversizedStroke(SignatureAttempt attempt, SignatureProcessingRules rules) {
            foreach (SignatureStrokeAttempt stroke in attempt.Strokes) {
                if (stroke != null && stroke.Points != null && stroke.Points.Count > rules.MaximumInputPointCount)
                    return true;
            }
            return false;
        }

        private static SignatureFailureReason DetermineFailure(SignatureAttempt attempt, SignatureProcessingRules rules) {
            bool allTooFew = true;
            foreach (SignatureStrokeAttempt stroke in attempt.Strokes) {
                int count = stroke == null || stroke.Points == null ? 0 : stroke.Points.Count;
                if (count > rules.MaximumInputPointCount) return SignatureFailureReason.TooManyPoints;
                if (count >= rules.MinimumUsablePointCountPerStroke) allTooFew = false;
            }
            return allTooFew ? SignatureFailureReason.TooFewPoints : SignatureFailureReason.NoUsableStrokes;
        }

        private static SignatureEvaluationResult Invalid(SignatureFailureReason reason) =>
            new(SignatureEvaluationStatus.InvalidAttempt, reason, 0f, 0f, EmptyBreakdown);
        private void EnsureInitialized() {
            if (!_initialized)
                throw new InvalidOperationException("SignatureEvaluator must be initialized before evaluation.");
        }
        public void Dispose() { }
    }
}
