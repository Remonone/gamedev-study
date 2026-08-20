using System;

namespace Services {
    public enum SignatureGuidancePhaseKind {
        Hidden = 0,
        Progressive = 1,
        Full = 2
    }

    public readonly struct SignatureGuidancePhase {
        public const float MaximumAlpha = 0.35f;
        public const float RevealDurationSeconds = 0.20f;

        public SignatureGuidancePhaseKind Kind { get; }
        public float Alpha { get; }

        public SignatureGuidancePhase(SignatureGuidancePhaseKind kind, float alpha) {
            Kind = kind;
            Alpha = alpha;
        }
    }

    public static class SignatureGuidancePhaseCalculator {
        public static SignatureGuidancePhase Calculate(double successfulSignatures,
            int fullDisplayAfterSignatures, int fadeOutSignatureCount) {
            double s = Math.Floor(Math.Max(0d, successfulSignatures));
            if (fullDisplayAfterSignatures > 0 && s < fullDisplayAfterSignatures) {
                return new SignatureGuidancePhase(SignatureGuidancePhaseKind.Progressive,
                    SignatureGuidancePhase.MaximumAlpha);
            }

            if (fadeOutSignatureCount > 0) {
                double fadeIndex = s - fullDisplayAfterSignatures;
                if (fadeIndex < fadeOutSignatureCount) {
                    float alpha = (float)(SignatureGuidancePhase.MaximumAlpha *
                        (fadeOutSignatureCount - fadeIndex) / fadeOutSignatureCount);
                    return new SignatureGuidancePhase(SignatureGuidancePhaseKind.Full, alpha);
                }
            }

            return new SignatureGuidancePhase(SignatureGuidancePhaseKind.Hidden, 0f);
        }
    }
}
