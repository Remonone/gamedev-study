using System;
using System.Linq;
using System.Reflection;
using Authoring;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Documents;
using Data.Enums;
using Data.Input;
using Data.Modifiers;
using Data.Requests;
using Data.Results;
using Data.Rules;
using NUnit.Framework;
using Services;
using Services.Calculators;
using Services.Locator;
using UnityEditor;
using UnityEngine;
using Utils.Metadata;

namespace Tests.EditMode {
    public sealed class SignatureDifficultySystemTests {
        private readonly System.Collections.Generic.List<UnityEngine.Object> _objects = new();

        [TearDown]
        public void TearDown() {
            foreach (UnityEngine.Object value in _objects) {
                if (value != null) UnityEngine.Object.DestroyImmediate(value);
            }
            _objects.Clear();
        }

        [Test]
        public void SignatureEntriesRoundTripAllDifficultyParameters() {
            SignatureDifficultyRules source = Rules("profile", 0.35f, 1.2f, 0.8f, 1.4f, 4f, 3f, 2f, 1f);

            SignatureDifficultyRules result = new SignatureEntries(source).ToRules(source.Id);

            Assert.That(result.Id, Is.EqualTo("profile"));
            Assert.That(result.MinimumSimilarity, Is.EqualTo(0.35f));
            Assert.That(result.CorridorWidthMultiplier, Is.EqualTo(1.2f));
            Assert.That(result.CoverageRequirementMultiplier, Is.EqualTo(0.8f));
            Assert.That(result.AlignmentToleranceMultiplier, Is.EqualTo(1.4f));
            Assert.That(result.ScoreWeights.CorridorFit, Is.EqualTo(4f));
            Assert.That(result.ScoreWeights.Coverage, Is.EqualTo(3f));
            Assert.That(result.ScoreWeights.Direction, Is.EqualTo(2f));
            Assert.That(result.ScoreWeights.StrokeStructure, Is.EqualTo(1f));
        }

        [Test]
        public void SignatureEntriesExposeAllBoundedModifierParameters() {
            IModifiableWrapper wrapper = MetadataWrapperFactory.CreateWrapper(typeof(SignatureEntries));
            string[] expected = {
                nameof(SignatureEntries.MinimumSimilarity),
                nameof(SignatureEntries.CorridorWidthMultiplier),
                nameof(SignatureEntries.CoverageRequirementMultiplier),
                nameof(SignatureEntries.AlignmentToleranceMultiplier),
                nameof(SignatureEntries.CorridorFitWeight),
                nameof(SignatureEntries.CoverageWeight),
                nameof(SignatureEntries.DirectionWeight),
                nameof(SignatureEntries.StrokeStructureWeight)
            };

            Assert.That(wrapper.Parameters.Select(parameter => parameter.Key.ParameterId), Is.EquivalentTo(expected));
            Assert.That(wrapper.TryGetParameter(nameof(SignatureEntries.MinimumSimilarity), out ICacheParameterMetadata threshold), Is.True);
            Assert.That(threshold.Minimum, Is.Zero);
            Assert.That(threshold.Maximum, Is.EqualTo(1d));
            Assert.That(wrapper.TryGetParameter(nameof(SignatureEntries.CorridorWidthMultiplier), out ICacheParameterMetadata width), Is.True);
            Assert.That(width.Minimum, Is.EqualTo((double)float.Epsilon));
            foreach (string parameterId in expected.Skip(2)) {
                Assert.That(wrapper.TryGetParameter(parameterId, out ICacheParameterMetadata parameter), Is.True);
                Assert.That(parameter.Minimum, Is.Zero, parameterId);
            }
        }

        [Test]
        public void CalculatorUsesSelectedPresetBaselineThenAppliesModifiers() {
            SelectedSignatureLoader loader = CreateLoader(CreatePreset(CreateProfile()));
            var modifier = new ThresholdModifierService(0.15f);
            var calculator = new SignatureCacheCalculator();
            var scope = new ServiceScope(null);
            scope.Register(loader).Register<IModifierService>(modifier);
            calculator.PreInitializeAsync(scope).GetAwaiter().GetResult();

            SignatureEntries result = calculator.Calculate();

            Assert.That(result.MinimumSimilarity, Is.EqualTo(0.55f).Within(0.0001f));
            Assert.That(result.CorridorWidthMultiplier, Is.EqualTo(1f));
            scope.Dispose();
        }

        [Test]
        public void InvalidatingSignatureCacheRecalculatesStashValue() {
            var cache = new CacheVersionService();
            var signatureCalculator = new CountingSignatureCalculator();
            var stash = new PlayerStatStash();
            var scope = new ServiceScope(null);
            scope.Register(cache, typeof(ICacheVersionProvider), typeof(ICacheInvalidator))
                .Register<ICacheCalculator<IncomeEntries>>(new StaticCalculator<IncomeEntries>(default))
                .Register<ICacheCalculator<SignatureEntries>>(signatureCalculator)
                .Register<ICacheCalculator<GenerationEntries>>(new StaticCalculator<GenerationEntries>(default))
                .Register<ICacheCalculator<OfficeEntries>>(new StaticCalculator<OfficeEntries>(default))
                .Register<ICacheCalculator<DocumentEntries>>(new StaticCalculator<DocumentEntries>(default))
                .Register(stash);
            stash.PreInitializeAsync(scope).GetAwaiter().GetResult();

            float first = stash.SignatureData.Value.MinimumSimilarity;
            ((ICacheInvalidator)cache).Invalidate<SignatureEntries>();
            float second = stash.SignatureData.Value.MinimumSimilarity;

            Assert.That(first, Is.EqualTo(1f));
            Assert.That(second, Is.EqualTo(2f));
            Assert.That(signatureCalculator.Count, Is.EqualTo(2));
            scope.Dispose();
        }

        [Test]
        public void LoaderReturnsDirectPresetAndCachesConfiguredBaseline() {
            SignatureDifficultyProfileDefinition profile = CreateProfile("configured");
            SignaturePresetDefinition preset = CreatePreset(profile);
            SelectedSignatureLoader loader = CreateLoader(preset);

            Assert.That(loader.GetActivePreset(), Is.SameAs(preset));
            Assert.That(loader.GetBaseDifficulty(), Is.SameAs(loader.GetBaseDifficulty()));
            Assert.That(loader.GetBaseDifficulty().Id, Is.EqualTo("configured"));
        }

        [Test]
        public void LoaderRejectsMissingPresetOrBaseProfile() {
            SelectedSignatureLoader missingPreset = CreateLoader(null);
            SelectedSignatureLoader missingProfile = CreateLoader(CreatePreset(null));

            Assert.That(() => missingPreset.GetActivePreset(), Throws.Exception);
            Assert.That(() => missingProfile.GetBaseDifficulty(), Throws.Exception);
        }

        [Test]
        public void NormalDocumentPolicySelectsEffectiveCachedDifficulty() {
            Type policyType = typeof(NormalDocumentProducer).GetNestedType(
                "PlayerDocumentEvaluationPolicy", BindingFlags.NonPublic);
            var policy = (IDocumentEvaluationPolicy)Activator.CreateInstance(policyType, nonPublic: true);
            SignatureDifficultyRules configured = Rules("profile", 0.4f, 1f, 1f, 1f, 1f, 1f, 1f, 1f);
            SignatureDifficultyRules effective = configured with { MinimumSimilarity = 0.2f, CorridorWidthMultiplier = 2f };

            DocumentEvaluationInputs result = policy.Resolve(new SignatureDifficultyContext(configured, effective));

            Assert.That(result.Difficulty, Is.SameAs(effective));
            AssertNone(result.Modifiers);
        }

        [Test]
        public void AcceptorForwardsPolicySelectedDifficultyAndNoLegacyModifiers() {
            SignatureDifficultyProfileDefinition profile = CreateProfile("configured");
            SignaturePresetDefinition preset = CreatePreset(profile);
            SelectedSignatureLoader loader = CreateLoader(preset);
            SignatureEntries effectiveEntries = new(profile.ToRules()) {
                MinimumSimilarity = 0.2f,
                CorridorWidthMultiplier = 2f
            };
            var evaluator = new CapturingEvaluator();
            var stash = new PlayerStatStash();
            var acceptor = new PlayerSignatureAcceptor();
            var scope = new ServiceScope(null);
            scope.Register(new CacheVersionService(), typeof(ICacheVersionProvider), typeof(ICacheInvalidator))
                .Register(loader)
                .Register<ICacheCalculator<IncomeEntries>>(new StaticCalculator<IncomeEntries>(default))
                .Register<ICacheCalculator<SignatureEntries>>(new StaticCalculator<SignatureEntries>(effectiveEntries))
                .Register<ICacheCalculator<GenerationEntries>>(new StaticCalculator<GenerationEntries>(default))
                .Register<ICacheCalculator<OfficeEntries>>(new StaticCalculator<OfficeEntries>(default))
                .Register<ICacheCalculator<DocumentEntries>>(new StaticCalculator<DocumentEntries>(default))
                .Register(stash)
                .Register<ISignatureEvaluator>(evaluator)
                .Register(acceptor);
            stash.PreInitializeAsync(scope).GetAwaiter().GetResult();
            stash.InitializeAsync(scope).GetAwaiter().GetResult();
            acceptor.InitializeAsync(scope).GetAwaiter().GetResult();
            var policy = new CapturingEffectivePolicy();
            var session = new FakeSession(policy);

            bool accepted = acceptor.AcceptSignature(new SignatureAttempt(Array.Empty<SignatureStrokeAttempt>(), 0f), session);

            Assert.That(accepted, Is.True);
            Assert.That(policy.Configured.MinimumSimilarity, Is.EqualTo(0.4f));
            Assert.That(policy.Effective.MinimumSimilarity, Is.EqualTo(0.2f));
            Assert.That(evaluator.Preset, Is.SameAs(preset));
            Assert.That(evaluator.Difficulty, Is.SameAs(policy.Effective));
            Assert.That(evaluator.Difficulty.Id, Is.EqualTo("configured"));
            AssertNone(evaluator.Modifiers);
            scope.Dispose();
        }

        private SignatureDifficultyProfileDefinition CreateProfile(string id = "profile") {
            SignatureDifficultyProfileDefinition profile = Track(ScriptableObject.CreateInstance<SignatureDifficultyProfileDefinition>());
            var serialized = new SerializedObject(profile);
            serialized.FindProperty("_id").stringValue = id;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }

        private SignaturePresetDefinition CreatePreset(SignatureDifficultyProfileDefinition profile) {
            SignaturePresetDefinition preset = Track(ScriptableObject.CreateInstance<SignaturePresetDefinition>());
            var serialized = new SerializedObject(preset);
            serialized.FindProperty("_id").stringValue = "preset";
            serialized.FindProperty("_baseDifficultyProfile").objectReferenceValue = profile;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return preset;
        }

        private SelectedSignatureLoader CreateLoader(SignaturePresetDefinition preset) {
            GameObject gameObject = Track(new GameObject("SelectedSignatureLoader"));
            SelectedSignatureLoader loader = gameObject.AddComponent<SelectedSignatureLoader>();
            var serialized = new SerializedObject(loader);
            serialized.FindProperty("_signaturePreset").objectReferenceValue = preset;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return loader;
        }

        private T Track<T>(T value) where T : UnityEngine.Object {
            _objects.Add(value);
            return value;
        }

        private static SignatureDifficultyRules Rules(string id, float threshold, float width, float coverage,
            float alignment, float fit, float coverageWeight, float direction, float structure) {
            return new SignatureDifficultyRules(id, threshold, width, coverage, alignment,
                new SignatureScoreWeights(fit, coverageWeight, direction, structure));
        }

        private static void AssertNone(SignatureRuleModifiers modifiers) {
            Assert.That(modifiers.CorridorWidthMultiplier, Is.EqualTo(1f));
            Assert.That(modifiers.MinimumSimilarityOffset, Is.Zero);
            Assert.That(modifiers.CoverageRequirementMultiplier, Is.EqualTo(1f));
            Assert.That(modifiers.AlignmentToleranceMultiplier, Is.EqualTo(1f));
            Assert.That(modifiers.DirectionContributionMultiplier, Is.EqualTo(1f));
        }

        private sealed class ThresholdModifierService : IModifierService, IService {
            private readonly float _offset;
            public ThresholdModifierService(float offset) => _offset = offset;
            public T Apply<T>(T value) where T : struct {
                if (value is not SignatureEntries entries) return value;
                entries.MinimumSimilarity += _offset;
                return (T)(object)entries;
            }
            public void Dispose() { }
        }

        private sealed class StaticCalculator<T> : ICacheCalculator<T>, IService where T : struct {
            private readonly T _value;
            public StaticCalculator(T value) => _value = value;
            public T Calculate() => _value;
            public void Dispose() { }
        }

        private sealed class CountingSignatureCalculator : ICacheCalculator<SignatureEntries>, IService {
            public int Count { get; private set; }
            public SignatureEntries Calculate() => new() { MinimumSimilarity = ++Count };
            public void Dispose() { }
        }

        private sealed class CapturingEffectivePolicy : IDocumentEvaluationPolicy {
            public SignatureDifficultyRules Configured { get; private set; }
            public SignatureDifficultyRules Effective { get; private set; }
            public DocumentEvaluationInputs Resolve(SignatureDifficultyContext difficulty) {
                Configured = difficulty.ConfiguredDifficulty;
                Effective = difficulty.EffectiveDifficulty;
                return new DocumentEvaluationInputs(Effective, SignatureRuleModifiers.None);
            }
        }

        private sealed class FakeSession : IDocumentSession {
            public FakeSession(IDocumentEvaluationPolicy policy) => EvaluationPolicy = policy;
            public DocumentKind Kind => DocumentKind.Normal;
            public IDocumentEvaluationPolicy EvaluationPolicy { get; }
            public bool TryProcess(SignatureEvaluationResult result) => true;
            public void Dispose() { }
        }

        private sealed class CapturingEvaluator : ISignatureEvaluator, IService {
            public SignaturePresetDefinition Preset { get; private set; }
            public SignatureDifficultyRules Difficulty { get; private set; }
            public SignatureRuleModifiers Modifiers { get; private set; }

            public SignatureEvaluationResult Evaluate(SignatureEvaluationRequest request) {
                return Evaluate(request.Attempt, request.Preset, request.Difficulty, request.Modifiers);
            }

            public SignatureEvaluationResult Evaluate(SignatureAttempt attempt, SignaturePresetDefinition preset,
                SignatureDifficultyRules difficulty, SignatureRuleModifiers modifiers) {
                Preset = preset;
                Difficulty = difficulty;
                Modifiers = modifiers;
                return new SignatureEvaluationResult(SignatureEvaluationStatus.Accepted, SignatureFailureReason.None,
                    1f, difficulty.MinimumSimilarity, new SignatureScoreBreakdown(1f, 1f, 1f, 1f, 1f));
            }

            public void Dispose() { }
        }
    }
}
