using System;
using System.Collections.Generic;
using Authoring;
using Services;
using UnityEngine;

namespace Presentation {
    public readonly struct StartingSignatureOption {
        public string Id { get; }
        public string DisplayName { get; }
        public IReadOnlyList<IReadOnlyList<Vector2>> PreviewStrokes { get; }

        public bool HasPreview => PreviewStrokes != null && PreviewStrokes.Count > 0;

        public StartingSignatureOption(string id, string displayName, IReadOnlyList<IReadOnlyList<Vector2>> previewStrokes) {
            Id = id;
            DisplayName = displayName;
            PreviewStrokes = previewStrokes ?? Array.Empty<IReadOnlyList<Vector2>>();
        }
    }

    public sealed class StartingSignatureSelectionViewModel : IDisposable {
        private readonly SignatureProgressionService _progression;
        private readonly List<StartingSignatureOption> _options = new(3);

        public IReadOnlyList<StartingSignatureOption> Options => _options;
        public bool IsSelectionRequired => _progression.RequiresStartingSelection;

        public StartingSignatureSelectionViewModel(SignatureProgressionService progression) {
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            for (int index = 0; progression.TryGetPendingPreset(index, out SignaturePresetDefinition preset); index++) {
                _options.Add(new StartingSignatureOption(preset.Id, preset.DisplayName, BuildPreviewStrokes(preset)));
            }
        }

        public bool Select(int index) {
            return index >= 0 && index < _options.Count &&
                   _progression.TrySelectStartingPreset(_options[index].Id);
        }

        public void Dispose() => _options.Clear();

        private static IReadOnlyList<IReadOnlyList<Vector2>> BuildPreviewStrokes(SignaturePresetDefinition preset) {
            if (preset?.Variants == null) return Array.Empty<IReadOnlyList<Vector2>>();

            for (int variantIndex = 0; variantIndex < preset.Variants.Count; variantIndex++) {
                SignatureVariantDefinition variant = preset.Variants[variantIndex];
                if (variant?.Strokes == null || variant.Strokes.Count == 0) continue;

                var previewStrokes = new List<IReadOnlyList<Vector2>>(variant.Strokes.Count);
                for (int strokeIndex = 0; strokeIndex < variant.Strokes.Count; strokeIndex++) {
                    SignatureTemplateStrokeDefinition stroke = variant.Strokes[strokeIndex];
                    if (stroke?.Nodes == null || stroke.Nodes.Count == 0) continue;

                    var points = new List<Vector2>(stroke.Nodes.Count);
                    for (int nodeIndex = 0; nodeIndex < stroke.Nodes.Count; nodeIndex++) {
                        SignatureCorridorNodeDefinition node = stroke.Nodes[nodeIndex];
                        if (node != null) points.Add(node.Position);
                    }

                    if (points.Count > 0) previewStrokes.Add(points);
                }

                if (previewStrokes.Count > 0) return previewStrokes;
            }

            return Array.Empty<IReadOnlyList<Vector2>>();
        }
    }
}
