using System;
using System.Collections.Generic;
using Authoring;
using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Templates;
using Exceptions;
using Services.Locator;
using UnityEngine;

namespace Services {
    public sealed class SignatureGuidanceService : IService, IInitialize {
        private GameStatisticsService _statistics;
        private SelectedSignatureLoader _signatureLoader;
        private ISignaturePresetRepository _repository;
        private bool _reminderConsumed;

        public UniTask InitializeAsync(IServiceScope scope) {
            _statistics = scope.Get<GameStatisticsService>();
            _signatureLoader = scope.Get<SelectedSignatureLoader>();
            _repository = scope.Get<ISignaturePresetRepository>();
            return UniTask.CompletedTask;
        }

        public bool TryGetSnapshot(out SignatureGuidanceSnapshot snapshot) {
            snapshot = null;
            if (_signatureLoader == null || _repository == null ||
                !_signatureLoader.TryGetActivePreset(out SignaturePresetDefinition preset)) {
                return false;
            }

            CompiledSignaturePreset compiled;
            try {
                compiled = _repository.GetOrCompile(preset);
            } catch (SignaturePresetConfigurationException) {
                return false;
            }

            if (compiled == null || compiled.Variants == null || compiled.Variants.Count == 0 ||
                !TryCopyFirstVariantGeometry(compiled.Variants[0], out var strokes)) {
                return false;
            }

            double successfulSignatures = 0d;
            _statistics?.TryGetValue(GameStatisticIds.DocumentsSuccessfullySigned, out successfulSignatures);
            SignatureGuidancePhase phase = SignatureGuidancePhaseCalculator.Calculate(successfulSignatures,
                compiled.GuidanceFullDisplayAfterSignatures, compiled.GuidanceFadeOutSignatureCount);

            if (!_reminderConsumed) {
                snapshot = new SignatureGuidanceSnapshot(true, SignatureGuidancePhaseKind.Full,
                    SignatureGuidancePhase.MaximumAlpha, strokes);
                return true;
            }

            snapshot = new SignatureGuidanceSnapshot(false, phase.Kind, phase.Alpha, strokes);
            return true;
        }

        public void ConsumeSessionReminder() {
            _reminderConsumed = true;
        }

        public void Dispose() {
            _statistics = null;
            _signatureLoader = null;
            _repository = null;
            _reminderConsumed = false;
        }

        private static bool TryCopyFirstVariantGeometry(SignatureTemplateVariant variant,
            out IReadOnlyList<IReadOnlyList<Vector2>> strokes) {
            strokes = null;
            if (variant == null || variant.Strokes == null || variant.Strokes.Count == 0) return false;

            var result = new List<IReadOnlyList<Vector2>>(variant.Strokes.Count);
            for (int strokeIndex = 0; strokeIndex < variant.Strokes.Count; strokeIndex++) {
                SignatureTemplateStroke stroke = variant.Strokes[strokeIndex];
                if (stroke == null || stroke.Nodes == null || stroke.Nodes.Count == 0) return false;

                var points = new Vector2[stroke.Nodes.Count];
                for (int pointIndex = 0; pointIndex < stroke.Nodes.Count; pointIndex++) {
                    Vector2 point = stroke.Nodes[pointIndex].Position;
                    if (!IsNormalized(point)) return false;
                    points[pointIndex] = point;
                }

                result.Add(Array.AsReadOnly(points));
            }

            strokes = Array.AsReadOnly(result.ToArray());
            return true;
        }

        private static bool IsNormalized(Vector2 point) {
            return IsFinite(point.x) && IsFinite(point.y) && point.x >= 0f && point.x <= 1f &&
                   point.y >= 0f && point.y <= 1f;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
