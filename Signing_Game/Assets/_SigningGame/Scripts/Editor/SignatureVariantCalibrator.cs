using System;
using System.Collections.Generic;
using System.Linq;
using Authoring;
using Contracts;
using Data.Processed;
using Data.Rules;
using UnityEditor;
using UnityEngine;

namespace SigningGame.Editor.Signatures {
    [Serializable]
    public sealed class SignatureCalibrationSettings {
        [SerializeField] private float _baseRadius = 0.025f;
        [SerializeField] private float _varianceMultiplier = 2f;
        [SerializeField] private float _minimumRadius = 0.01f;
        [SerializeField] private float _maximumRadius = 0.25f;
        [SerializeField] private int _radiusSmoothingPasses = 1;
        public float BaseRadius => _baseRadius;
        public float VarianceMultiplier => _varianceMultiplier;
        public float MinimumRadius => _minimumRadius;
        public float MaximumRadius => _maximumRadius;
        public int RadiusSmoothingPasses => _radiusSmoothingPasses;
    }

    public sealed class SignatureCalibrationSampleDiagnostic {
        public string SampleId { get; }
        public float MeanDeviation { get; }
        public bool LikelyOutlier { get; }
        public SignatureCalibrationSampleDiagnostic(string sampleId, float meanDeviation, bool likelyOutlier) {
            SampleId = sampleId; MeanDeviation = meanDeviation; LikelyOutlier = likelyOutlier;
        }
    }

    public sealed class SignatureCalibrationResult {
        public bool Success { get; }
        public string Message { get; }
        public bool RecommendsSeparateVariant { get; }
        public IReadOnlyList<SignatureCalibrationSampleDiagnostic> Diagnostics { get; }
        private SignatureCalibrationResult(bool success, string message, bool recommendsSeparateVariant,
            IReadOnlyList<SignatureCalibrationSampleDiagnostic> diagnostics) {
            Success = success; Message = message; RecommendsSeparateVariant = recommendsSeparateVariant;
            Diagnostics = diagnostics ?? Array.Empty<SignatureCalibrationSampleDiagnostic>();
        }
        public static SignatureCalibrationResult Failed(string message, bool separateVariant = false) =>
            new(false, message, separateVariant, null);
        public static SignatureCalibrationResult Completed(IReadOnlyList<SignatureCalibrationSampleDiagnostic> diagnostics) =>
            new(true, "Calibration completed.", false, diagnostics);
    }

    public sealed class SignatureVariantCalibrator {
        public SignatureCalibrationResult Compile(SignatureSampleSet sampleSet, string variantId,
            SignatureCalibrationSettings settings, ISignaturePreprocessor preprocessor) {
            if (string.IsNullOrWhiteSpace(variantId)) return SignatureCalibrationResult.Failed("A variant name is required.");
            if (preprocessor == null) throw new ArgumentNullException(nameof(preprocessor));
            string settingsError = ValidateSettings(settings);
            if (settingsError != null) return SignatureCalibrationResult.Failed(settingsError);
            SignaturePresetDefinition preset = sampleSet.TargetPreset;
            SignatureProcessingProfileDefinition profile = preset.ProcessingProfile;
            if (profile == null) return SignatureCalibrationResult.Failed("The target preset requires a processing profile.");
            SignatureVariantDefinition target = new SignatureVariantDefinition();
            if (preset.Variants.Any(variant => variantId.Equals(variant.Id))) 
                return SignatureCalibrationResult.Failed("The target VariantId must identify exactly one existing variant.");
            
            if (sampleSet.Samples.Count < 2) return SignatureCalibrationResult.Failed("At least two enabled samples are required.");

            SignatureProcessingRules rules = profile.ToRules();
            var geometries = new List<SampleGeometry>(sampleSet.Samples.Count);
            foreach (RecordedSignatureSampleDefinition sample in sampleSet.Samples) {
                ProcessedSignature processed = preprocessor.Process(sample.ToAttempt(), rules);
                if (processed == null)
                    return SignatureCalibrationResult.Failed($"Sample '{sample.Id}' is invalid and could not be processed.", true);
                geometries.Add(new SampleGeometry(sample.Id, processed));
            }

            bool initializesEmpty = target.Strokes == null || target.Strokes.Count == 0;
            int strokeCount = geometries[0].Strokes.Length;
            foreach (SampleGeometry geometry in geometries) {
                for (int s = 0; s < strokeCount; s++)
                    if (geometry.Strokes[s].Length != rules.ResampledPointCountPerStroke)
                        return SignatureCalibrationResult.Failed("Enabled samples have a node-count mismatch; create a separate variant.", true);
            }
            if (!initializesEmpty) {
                for (int s = 0; s < strokeCount; s++) {
                    SignatureTemplateStrokeDefinition stroke = target.Strokes[s];
                    if (stroke == null || stroke.Nodes == null || stroke.Nodes.Count != rules.ResampledPointCountPerStroke)
                        return SignatureCalibrationResult.Failed("Target variant node shape does not match samples; create a separate variant.", true);
                }
            }

            OrientSamples(geometries, target, initializesEmpty);
            Vector2[][] centers = CalculateCenters(geometries, strokeCount, rules.ResampledPointCountPerStroke);
            float[][] radii = CalculateRadii(geometries, centers, settings);
            IReadOnlyList<SignatureCalibrationSampleDiagnostic> diagnostics = CalculateDiagnostics(geometries, centers);

            Undo.RecordObject(preset, "Calibrate Signature Variant");
            var serializedPreset = new SerializedObject(preset);
            SerializedProperty variants = serializedPreset.FindProperty("_variants");
            var index = variants.arraySize;
            variants.InsertArrayElementAtIndex(index);
            SerializedProperty variant = variants.GetArrayElementAtIndex(index);
            SerializedProperty strokesProperty = variant.FindPropertyRelative("_strokes");
            if (initializesEmpty) InitializeStrokes(strokesProperty, centers, radii);
            else UpdateExistingStrokes(strokesProperty, centers, radii);
            serializedPreset.ApplyModifiedProperties();
            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssets();
            return SignatureCalibrationResult.Completed(diagnostics);
        }

        private static string ValidateSettings(SignatureCalibrationSettings settings) {
            if (!Finite(settings.BaseRadius) || !Finite(settings.VarianceMultiplier) || !Finite(settings.MinimumRadius) ||
                !Finite(settings.MaximumRadius)) return "All calibration float settings must be finite.";
            if (settings.BaseRadius < 0f) return "BaseRadius must be nonnegative.";
            if (settings.VarianceMultiplier < 0f) return "VarianceMultiplier must be nonnegative.";
            if (settings.MinimumRadius <= 0f) return "MinRadius must be positive.";
            if (settings.MaximumRadius < settings.MinimumRadius) return "MaxRadius must be at least MinRadius.";
            if (settings.RadiusSmoothingPasses < 0) return "Radius smoothing passes must be nonnegative.";
            return null;
        }

        private static void OrientSamples(List<SampleGeometry> samples, SignatureVariantDefinition target, bool empty) {
            Vector2[][] reference = empty ? Clone(samples[0].Strokes) : ExistingCenterline(target);
            for (int sampleIndex = empty ? 1 : 0; sampleIndex < samples.Count; sampleIndex++) {
                for (int strokeIndex = 0; strokeIndex < samples[sampleIndex].Strokes.Length; strokeIndex++) {
                    bool mayReverse = empty || target.Strokes[strokeIndex].AllowReverseDirection;
                    if (!mayReverse) continue;
                    Vector2[] points = samples[sampleIndex].Strokes[strokeIndex];
                    float forward = SquaredDistance(points, reference[strokeIndex], false);
                    float reverse = SquaredDistance(points, reference[strokeIndex], true);
                    if (reverse < forward) Array.Reverse(points);
                }
            }
        }

        private static Vector2[][] CalculateCenters(List<SampleGeometry> samples, int strokes, int nodes) {
            var result = new Vector2[strokes][];
            for (int s = 0; s < strokes; s++) {
                result[s] = new Vector2[nodes];
                for (int n = 0; n < nodes; n++) {
                    Vector2 sum = Vector2.zero;
                    foreach (SampleGeometry sample in samples) sum += sample.Strokes[s][n];
                    result[s][n] = sum / samples.Count;
                }
            }
            return result;
        }

        private static float[][] CalculateRadii(List<SampleGeometry> samples, Vector2[][] centers,
            SignatureCalibrationSettings settings) {
            var radii = new float[centers.Length][];
            for (int s = 0; s < centers.Length; s++) {
                radii[s] = new float[centers[s].Length];
                for (int n = 0; n < centers[s].Length; n++) {
                    float squared = 0f;
                    foreach (SampleGeometry sample in samples) squared += (sample.Strokes[s][n] - centers[s][n]).sqrMagnitude;
                    float sigma = Mathf.Sqrt(squared / samples.Count);
                    radii[s][n] = Mathf.Clamp(settings.BaseRadius + settings.VarianceMultiplier * sigma,
                        settings.MinimumRadius, settings.MaximumRadius);
                }
                var source = (float[])radii[s].Clone();
                var destination = new float[source.Length];
                for (int pass = 0; pass < settings.RadiusSmoothingPasses; pass++) {
                    destination[0] = Mathf.Clamp(source[0], settings.MinimumRadius, settings.MaximumRadius);
                    destination[^1] = Mathf.Clamp(source[^1], settings.MinimumRadius, settings.MaximumRadius);
                    for (int n = 1; n < source.Length - 1; n++)
                        destination[n] = Mathf.Clamp(source[n - 1] * 0.25f + source[n] * 0.5f + source[n + 1] * 0.25f,
                            settings.MinimumRadius, settings.MaximumRadius);
                    var swap = source; source = destination; destination = swap;
                }
                radii[s] = source;
            }
            return radii;
        }

        private static IReadOnlyList<SignatureCalibrationSampleDiagnostic> CalculateDiagnostics(
            List<SampleGeometry> samples, Vector2[][] centers) {
            var deviations = new float[samples.Count];
            for (int i = 0; i < samples.Count; i++) {
                float sum = 0f; int count = 0;
                for (int s = 0; s < centers.Length; s++) for (int n = 0; n < centers[s].Length; n++) {
                    sum += Vector2.Distance(samples[i].Strokes[s][n], centers[s][n]); count++;
                }
                deviations[i] = count == 0 ? 0f : sum / count;
            }
            float mean = 0f; foreach (float deviation in deviations) mean += deviation; mean /= deviations.Length;
            float variance = 0f; foreach (float deviation in deviations) variance += (deviation - mean) * (deviation - mean);
            float threshold = mean + 2f * Mathf.Sqrt(variance / deviations.Length);
            var result = new List<SignatureCalibrationSampleDiagnostic>(samples.Count);
            for (int i = 0; i < samples.Count; i++)
                result.Add(new SignatureCalibrationSampleDiagnostic(samples[i].Id, deviations[i], deviations[i] > threshold));
            result.Sort((a, b) => { int compare = b.MeanDeviation.CompareTo(a.MeanDeviation);
                return compare != 0 ? compare : string.CompareOrdinal(a.SampleId, b.SampleId); });
            return result;
        }

        private static SerializedProperty FindVariant(SerializedProperty variants, string id) {
            for (int i = 0; i < variants.arraySize; i++) {
                
                SerializedProperty variant = variants.GetArrayElementAtIndex(i);
                if (variant.FindPropertyRelative("_id").stringValue == id) return variant;
            }
            return null;
        }

        private static void InitializeStrokes(SerializedProperty strokes, Vector2[][] centers, float[][] radii) {
            strokes.arraySize = centers.Length;
            for (int s = 0; s < centers.Length; s++) {
                SerializedProperty stroke = strokes.GetArrayElementAtIndex(s);
                stroke.FindPropertyRelative("_id").stringValue = $"stroke-{s + 1}";
                stroke.FindPropertyRelative("_required").boolValue = true;
                stroke.FindPropertyRelative("_importance").floatValue = 1f;
                stroke.FindPropertyRelative("_minimumCoverage").floatValue = 0.8f;
                stroke.FindPropertyRelative("_allowReverseDirection").boolValue = false;
                stroke.FindPropertyRelative("_directionImportance").floatValue = 1f;
                SerializedProperty nodes = stroke.FindPropertyRelative("_nodes"); nodes.arraySize = centers[s].Length;
                for (int n = 0; n < centers[s].Length; n++) SetNode(nodes.GetArrayElementAtIndex(n), centers[s][n], radii[s][n], true);
            }
        }

        private static void UpdateExistingStrokes(SerializedProperty strokes, Vector2[][] centers, float[][] radii) {
            for (int s = 0; s < centers.Length; s++) {
                SerializedProperty nodes = strokes.GetArrayElementAtIndex(s).FindPropertyRelative("_nodes");
                for (int n = 0; n < centers[s].Length; n++) SetNode(nodes.GetArrayElementAtIndex(n), centers[s][n], radii[s][n], false);
            }
        }
        private static void SetNode(SerializedProperty node, Vector2 position, float radius, bool initializeImportance) {
            node.FindPropertyRelative("_position").vector2Value = position;
            node.FindPropertyRelative("_radius").floatValue = radius;
            if (initializeImportance) node.FindPropertyRelative("_importance").floatValue = 1f;
        }

        private static Vector2[][] ExistingCenterline(SignatureVariantDefinition target) {
            var result = new Vector2[target.Strokes.Count][];
            for (int s = 0; s < result.Length; s++) { result[s] = new Vector2[target.Strokes[s].Nodes.Count];
                for (int n = 0; n < result[s].Length; n++) result[s][n] = target.Strokes[s].Nodes[n].Position; }
            return result;
        }
        private static Vector2[][] Clone(Vector2[][] source) { var result = new Vector2[source.Length][];
            for (int i = 0; i < source.Length; i++) result[i] = (Vector2[])source[i].Clone(); return result; }
        private static float SquaredDistance(Vector2[] sample, Vector2[] reference, bool reverse) { float sum = 0f;
            for (int i = 0; i < sample.Length; i++) sum += (sample[reverse ? sample.Length - 1 - i : i] - reference[i]).sqrMagnitude; return sum; }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private sealed class SampleGeometry {
            public readonly string Id; public readonly Vector2[][] Strokes;
            public SampleGeometry(string id, ProcessedSignature processed) { Id = id; Strokes = new Vector2[processed.Strokes.Count][];
                for (int s = 0; s < Strokes.Length; s++) { Strokes[s] = new Vector2[processed.Strokes[s].Points.Count];
                    for (int n = 0; n < Strokes[s].Length; n++) Strokes[s][n] = processed.Strokes[s].Points[n].Position; } }
        }
    }
}
