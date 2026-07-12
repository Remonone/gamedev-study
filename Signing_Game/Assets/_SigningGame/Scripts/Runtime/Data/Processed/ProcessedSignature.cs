using System.Collections.Generic;
using UnityEngine;

namespace Data.Processed {
    public sealed class ProcessedSignature {
        public IReadOnlyList<ProcessedSignatureStroke> Strokes { get; }
        public Rect Bounds { get; }
        public float TotalLength { get; }

        public ProcessedSignature(IReadOnlyList<ProcessedSignatureStroke> strokes,
            Rect bounds,
            float totalLength) {
            Strokes = strokes;
            Bounds = bounds;
            TotalLength = totalLength;
        }
    }
}