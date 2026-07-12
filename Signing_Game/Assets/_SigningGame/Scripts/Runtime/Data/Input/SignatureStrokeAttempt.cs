using System;
using System.Collections.Generic;

namespace Data.Input {
    public sealed class SignatureStrokeAttempt {
        public IReadOnlyList<SignatureInputPoint> Points { get; }

        public SignatureStrokeAttempt(IReadOnlyList<SignatureInputPoint> points)
        {
            Points = points
                     ?? throw new ArgumentNullException(nameof(points));
        }
    }
}