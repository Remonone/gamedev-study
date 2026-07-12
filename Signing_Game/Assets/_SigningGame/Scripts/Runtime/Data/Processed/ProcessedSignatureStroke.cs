using System.Collections.Generic;

namespace Data.Processed {
    public sealed class ProcessedSignatureStroke {
        public IReadOnlyList<ProcessedSignaturePoint> Points { get; }
        public float Length { get; }

        public ProcessedSignatureStroke(IReadOnlyList<ProcessedSignaturePoint> points, float length) {
            Points = points;
            Length = length;
        }
    }
}