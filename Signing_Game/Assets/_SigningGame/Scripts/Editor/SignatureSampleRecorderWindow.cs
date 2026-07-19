using System;
using System.Linq;
using Authoring;
using Data.Input;
using Data.Processed;
using Services;
using UnityEditor;
using UnityEngine;

namespace SigningGame.Editor.Signatures {
    public sealed class SignatureSampleRecorderWindow : EditorWindow {
        
        [SerializeField] private SignatureCalibrationSettings _calibrationSettings = new();
        [SerializeField] private string _variantName;
        [SerializeField] private bool _drawProcessedPreview = true;
        private readonly SignatureEditorDrawingCanvas _canvas = new();
        private SignaturePreprocessor _preprocessor;
        private SignatureCalibrationResult _calibrationResult;
        private SignatureSampleSet _sampleSet = new();
        
        [SerializeField] private SignaturePresetDefinition _targetPreset;

        [MenuItem("Game/Signatures/Sample Recorder")]
        private static void Open() => GetWindow<SignatureSampleRecorderWindow>("Signature Samples");
        private void OnEnable() => _preprocessor ??= new SignaturePreprocessor();
        private void OnDisable() { _preprocessor?.Dispose(); _preprocessor = null; }
        private void OnDestroy() { _preprocessor?.Dispose(); _preprocessor = null; }

        private void OnGUI() {
            _targetPreset = (SignaturePresetDefinition)EditorGUILayout.ObjectField("Target Signature Definition", _targetPreset,
                typeof(SignaturePresetDefinition), false);
            var windowState = new SerializedObject(this);
            windowState.Update();

            EditorGUILayout.PropertyField(windowState.FindProperty("_calibrationSettings"), true); 
            EditorGUILayout.PropertyField(windowState.FindProperty("_variantName"), true);
            EditorGUILayout.PropertyField(windowState.FindProperty("_drawProcessedPreview"), true);
            windowState.ApplyModifiedPropertiesWithoutUndo();
            Rect canvasRect = GUILayoutUtility.GetRect(200f, 300f, GUILayout.ExpandWidth(true));
            _canvas.DrawAndCapture(canvasRect);
            EditorGUILayout.LabelField("Raw strokes", _canvas.Strokes.Count.ToString());
            int pointCount = 0; foreach (var stroke in _canvas.Strokes) pointCount += stroke.Count;
            EditorGUILayout.LabelField("Raw points", pointCount.ToString());
            if(_drawProcessedPreview) DrawProcessedPreview(canvasRect);
            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Clear")) _canvas.Clear();
                if (GUILayout.Button("Save / Append")) AppendSample();
                if (GUILayout.Button("Compile")) Compile();
            }
            DrawSamples();
            if (_calibrationResult != null) {
                EditorGUILayout.HelpBox(_calibrationResult.Message,
                    _calibrationResult.Success ? MessageType.Info : MessageType.Error);
                foreach (SignatureCalibrationSampleDiagnostic diagnostic in _calibrationResult.Diagnostics)
                    EditorGUILayout.LabelField(diagnostic.SampleId,
                        $"{diagnostic.MeanDeviation:F5}" + (diagnostic.LikelyOutlier ? "  LIKELY OUTLIER" : string.Empty));
            }
            Repaint();
        }

        private void AppendSample() {
            SignatureAttempt attempt = _canvas.ToAttempt();
            if (!TryAppendSample(attempt, out string error)) {
                EditorUtility.DisplayDialog("Cannot save sample", error, "OK");
                return;
            }
            _canvas.Clear();
        }

        private bool TryAppendSample(SignatureAttempt attempt, out string error) {
            _sampleSet.SetTargetPreset(_targetPreset);
            if (!TryCurrentProfile(out SignatureProcessingProfileDefinition profile, out error)) return false;
            if (attempt == null || attempt.Strokes.Count == 0) {
                error = "The drawing is empty or invalid for the current processing profile.";
                return false;
            }
            _preprocessor ??= new SignaturePreprocessor();
            ProcessedSignature processed = _preprocessor.Process(attempt, profile.ToRules());
            if (processed == null) {
                error = "The drawing is empty or invalid for the current processing profile.";
                return false;
            }
            var sample = new RecordedSignatureSampleDefinition(Guid.NewGuid().ToString("N"), null);
           
            for (int s = 0; s < attempt.Strokes.Count; s++) {
                var _id = Guid.NewGuid().ToString("N");
                var points = attempt.Strokes[s].Points;
                var recordedPoints = new RecordedSignaturePointDefinition[attempt.Strokes[s].Points.Count];
                for (int i = 0; i < points.Count; i++) {
                    var point = points[i];
                    var recordedPoint = new RecordedSignaturePointDefinition(_id, point.Position, point.Time);
                    recordedPoints[i] = recordedPoint;
                }
                var stroke = new RecordedSignatureStrokeDefinition(_id, recordedPoints.ToList());
                sample.AddStroke(stroke);
            }
            
            _sampleSet.AddSample(sample);
            error = null;
            return true;
        }

        private bool TryCurrentProfile(out SignatureProcessingProfileDefinition profile, out string error) {
            profile = null; error = null;
            if (_sampleSet == null) { error = "Assign a sample set."; return false; }
            if (_sampleSet.TargetPreset == null || _sampleSet.TargetPreset.ProcessingProfile == null) {
                error = "The sample set target preset and its processing profile are required."; return false;
            }
            profile = _sampleSet.TargetPreset.ProcessingProfile;
            return true;
        }

        private void DrawSamples() {
            if (_sampleSet == null) return;
            var samples = _sampleSet.Samples;
            for (int i = 0; i < samples.Count; i++) {
                RecordedSignatureSampleDefinition sample = samples[i];
                using (new EditorGUILayout.HorizontalScope()) {
                    EditorGUILayout.TextField(sample.Id);
                }
                EditorGUILayout.LabelField($"{sample.Strokes.Count} strokes, {sample.Strokes.Sum((t) => t.Points.Count)} raw points");
            }
        }

        private void DrawProcessedPreview(Rect rect) {
            if (_sampleSet == null || _canvas.Strokes.Count == 0 || !TryCurrentProfile(out var profile, out _)) return;
            ProcessedSignature processed;
            try { processed = _preprocessor.Process(_canvas.ToAttempt(), profile.ToRules()); } catch { return; }
            if (processed == null) return;
            Handles.BeginGUI(); Handles.color = Color.cyan;
            foreach (ProcessedSignatureStroke stroke in processed.Strokes) for (int i = 1; i < stroke.Points.Count; i++)
                Handles.DrawLine(Map(rect, stroke.Points[i - 1].Position), Map(rect, stroke.Points[i].Position));
            Handles.EndGUI();
        }
        private void Compile() {
            _calibrationResult = new SignatureVariantCalibrator().Compile(_sampleSet, _variantName, _calibrationSettings, _preprocessor);
        }
        private static Vector2 Map(Rect rect, Vector2 p) => new(rect.x + p.x * rect.width, rect.y + (1f - p.y) * rect.height);
    }
}
