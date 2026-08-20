using System;
using System.Collections.Generic;
using UnityEngine;

namespace Services {
    public sealed class SignatureGuidanceSnapshot {
        public bool IsSessionReminder { get; }
        public SignatureGuidancePhaseKind Phase { get; }
        public float Alpha { get; }
        public IReadOnlyList<IReadOnlyList<Vector2>> Strokes { get; }

        public SignatureGuidanceSnapshot(bool isSessionReminder, SignatureGuidancePhaseKind phase,
            float alpha, IReadOnlyList<IReadOnlyList<Vector2>> strokes) {
            if (strokes == null) throw new ArgumentNullException(nameof(strokes));

            IsSessionReminder = isSessionReminder;
            Phase = phase;
            Alpha = alpha;

            var strokeSnapshot = new IReadOnlyList<Vector2>[strokes.Count];
            for (int strokeIndex = 0; strokeIndex < strokes.Count; strokeIndex++) {
                IReadOnlyList<Vector2> sourceStroke = strokes[strokeIndex]
                    ?? throw new ArgumentException("Guidance contains a null stroke.", nameof(strokes));
                var pointSnapshot = new Vector2[sourceStroke.Count];
                for (int pointIndex = 0; pointIndex < sourceStroke.Count; pointIndex++) {
                    pointSnapshot[pointIndex] = sourceStroke[pointIndex];
                }

                strokeSnapshot[strokeIndex] = Array.AsReadOnly(pointSnapshot);
            }

            Strokes = Array.AsReadOnly(strokeSnapshot);
        }
    }
}
