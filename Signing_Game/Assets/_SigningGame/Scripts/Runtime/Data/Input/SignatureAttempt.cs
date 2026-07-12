using System;
using System.Collections.Generic;

namespace Data.Input {
    public sealed class SignatureAttempt {
        public IReadOnlyList<SignatureStrokeAttempt> Strokes { get; }
        public float Duration { get; }

        public SignatureAttempt(IReadOnlyList<SignatureStrokeAttempt> strokes, float duration) {
            Strokes = strokes
                      ?? throw new ArgumentNullException(nameof(strokes));

            Duration = duration;
        }
    }
}