using Authoring;
using Contracts;
using Data.Enums;
using Data.Processed;
using Data.Results;
using Data.Rules;
using Data.Templates;
using Services;
using Services.Locator;
using UnityEditor;
using UnityEngine;

namespace SigningGame.Editor.Signatures {
    public sealed class SignaturePresetTestWindow : EditorWindow {
        [SerializeField] private SignaturePresetDefinition _preset;
        [SerializeField] private SignatureDifficultyProfileDefinition _difficulty;
        private readonly SignatureEditorDrawingCanvas _canvas = new();
        private SignaturePreprocessor _preprocessor;
        private SignaturePresetCompiler _compiler;
        private SignaturePresetRepository _repository;
        private RuleResolver _resolver;
        private SignatureMatcher _matcher;
        private SignatureEvaluator _evaluator;
        private ServiceScope _serviceScope;
        private SignatureEvaluationResult _result;
        private ProcessedSignature _processed;
        private CompiledSignaturePreset _compiled;
        private int _observedCanvasRevision;
        private float _evaluatedCorridorWidthMultiplier = 1f;

        [MenuItem("Game/Signatures/Preset Test")]
        private static void Open() => GetWindow<SignaturePresetTestWindow>("Signature Test");
        private void OnEnable() => EnsureServices();
        private void OnDisable() => DisposeServices();
        private void OnDestroy() => DisposeServices();

        private void OnGUI() {
            EditorGUI.BeginChangeCheck();
            SignaturePresetDefinition preset = (SignaturePresetDefinition)EditorGUILayout.ObjectField("Preset", _preset,
                typeof(SignaturePresetDefinition), false);
            SignatureDifficultyProfileDefinition difficulty = (SignatureDifficultyProfileDefinition)EditorGUILayout.ObjectField(
                "Difficulty", _difficulty, typeof(SignatureDifficultyProfileDefinition), false);
            if (EditorGUI.EndChangeCheck()) {
                _preset = preset;
                _difficulty = difficulty;
                ClearEvaluationState();
            }
            Rect rect = GUILayoutUtility.GetRect(200f, 320f, GUILayout.ExpandWidth(true));
            _canvas.DrawAndCapture(rect);
            if (_observedCanvasRevision != _canvas.Revision) {
                _observedCanvasRevision = _canvas.Revision;
                ClearEvaluationState();
            }
            DrawOverlay(rect);
            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Clear")) {
                    _canvas.Clear();
                    _observedCanvasRevision = _canvas.Revision;
                    ClearEvaluationState();
                }
                if (GUILayout.Button("Evaluate")) Evaluate();
            }
            if (_result != null) {
                EditorGUILayout.LabelField("Status", DisplayStatus(_result.Status));
                EditorGUILayout.LabelField("Failure", _result.FailureReason.ToString());
                EditorGUILayout.LabelField("Similarity", _result.Similarity.ToString("F4"));
                EditorGUILayout.LabelField("Quality", _result.Quality.ToString("F4"));
                EditorGUILayout.LabelField("Minimum Similarity Threshold", _result.MinimumSimilarity.ToString("F4"));
                EditorGUILayout.LabelField("Matched Variant", _result.MatchedVariantId ?? "-");
                SignatureScoreBreakdown breakdown = _result.ScoreBreakdown;
                EditorGUILayout.LabelField("Corridor Fit", breakdown.CorridorFit.ToString("F4"));
                EditorGUILayout.LabelField("Coverage", breakdown.Coverage.ToString("F4"));
                EditorGUILayout.LabelField("Direction", breakdown.Direction.ToString("F4"));
                EditorGUILayout.LabelField("Stroke Structure", breakdown.StrokeStructure.ToString("F4"));
                EditorGUILayout.LabelField("Total", breakdown.Total.ToString("F4"));
                foreach (SignatureStrokeMatchResult stroke in _result.StrokeResults)
                    EditorGUILayout.LabelField("Stroke",
                        $"input index {stroke.InputStrokeIndex}, template ID {stroke.TemplateStrokeId}, " +
                        $"fit {stroke.CorridorFit:F3}, coverage {stroke.Coverage:F3}, " +
                        $"direction {stroke.Direction:F3}, similarity {stroke.Similarity:F3}");
            }
            Repaint();
        }

        private void Evaluate() {
            ClearEvaluationState();
            if (_preset == null || _difficulty == null) {
                ShowNotification(new GUIContent("Assign preset and difficulty."));
                return;
            }
            try {
                EnsureServices();
                Data.Input.SignatureAttempt attempt = _canvas.ToAttempt();
                SignatureDifficultyRules difficultyRules = _difficulty.ToRules();
                _repository.Invalidate(_preset);
                _compiled = _repository.GetOrCompile(_preset);
                _processed = _preprocessor.Process(attempt, _compiled.Processing);
                SignatureEvaluationResult result = _evaluator.Evaluate(attempt, _preset, difficultyRules,
                    SignatureRuleModifiers.None);
                _evaluatedCorridorWidthMultiplier = difficultyRules.CorridorWidthMultiplier *
                                                    SignatureRuleModifiers.None.CorridorWidthMultiplier;
                _result = result;
            } catch (System.Exception exception) {
                ClearEvaluationState();
                Debug.LogException(exception);
            }
        }

        private void DrawOverlay(Rect rect) {
            if (_result == null || _compiled == null) return;
            Handles.BeginGUI();
            SignatureTemplateVariant matchedVariant = null;
            foreach (SignatureTemplateVariant candidate in _compiled.Variants)
                if (candidate.Id == _result.MatchedVariantId) { matchedVariant = candidate; break; }
            if (matchedVariant != null) foreach (SignatureTemplateStroke stroke in matchedVariant.Strokes)
                for (int i = 0; i < stroke.Nodes.Count; i++) {
                    SignatureCorridorNode node = stroke.Nodes[i]; Vector2 point = Map(rect, node.Position);
                    Handles.color = new Color(1f, 0.65f, 0f, 0.35f); Handles.DrawWireDisc(point, Vector3.forward,
                        node.Radius * _evaluatedCorridorWidthMultiplier * Mathf.Min(rect.width, rect.height));
                    if (i > 0) Handles.DrawLine(Map(rect, stroke.Nodes[i - 1].Position), point);
                }
            if (_processed != null) { Handles.color = Color.cyan; foreach (ProcessedSignatureStroke stroke in _processed.Strokes)
                for (int i = 1; i < stroke.Points.Count; i++) Handles.DrawLine(Map(rect, stroke.Points[i - 1].Position), Map(rect, stroke.Points[i].Position)); }
            Handles.EndGUI();
        }

        private void ClearEvaluationState() {
            _result = null;
            _processed = null;
            _compiled = null;
            _evaluatedCorridorWidthMultiplier = 1f;
        }
        private static string DisplayStatus(SignatureEvaluationStatus status) => status switch {
            SignatureEvaluationStatus.Accepted => "Accepted",
            SignatureEvaluationStatus.Rejected => "Rejected",
            SignatureEvaluationStatus.InvalidAttempt => "Invalid",
            _ => status.ToString()
        };

        private void EnsureServices() {
            if (_evaluator != null) return;
            try {
                _preprocessor = new SignaturePreprocessor();
                _compiler = new SignaturePresetCompiler();
                _repository = new SignaturePresetRepository();
                _resolver = new RuleResolver();
                _matcher = new SignatureMatcher();
                _evaluator = new SignatureEvaluator();
                _serviceScope = new ServiceScope();
                _serviceScope.Register(_preprocessor).Register(typeof(ISignaturePreprocessor), _preprocessor)
                    .Register(_compiler).Register(typeof(ISignaturePresetCompiler), _compiler)
                    .Register(_repository).Register(typeof(ISignaturePresetRepository), _repository)
                    .Register(_resolver).Register(typeof(ISignatureRulesResolver), _resolver)
                    .Register(_matcher).Register(typeof(ISignatureMatcher), _matcher)
                    .Register(_evaluator).Register(typeof(ISignatureEvaluator), _evaluator);
                Awaitable initialization = _serviceScope.InitializeAsync(_serviceScope);
                initialization.GetAwaiter().GetResult();
            } catch {
                DisposeServices();
                throw;
            }
        }
        private void DisposeServices() {
            _serviceScope?.Dispose();
            _serviceScope = null;
            _evaluator = null; _matcher = null; _resolver = null; _repository = null; _compiler = null; _preprocessor = null;
        }
        private static Vector2 Map(Rect rect, Vector2 p) => new(rect.x + p.x * rect.width, rect.y + (1f - p.y) * rect.height);
    }
}
