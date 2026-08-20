using NUnit.Framework;
using Services;

namespace Tests.EditMode {
    public sealed class SignatureGuidancePhaseTests {
        [TestCase(-5d, 3, 5, SignatureGuidancePhaseKind.Progressive, 0.35f)]
        [TestCase(0d, 3, 5, SignatureGuidancePhaseKind.Progressive, 0.35f)]
        [TestCase(2.99d, 3, 5, SignatureGuidancePhaseKind.Progressive, 0.35f)]
        [TestCase(3d, 3, 5, SignatureGuidancePhaseKind.Full, 0.35f)]
        [TestCase(3.9d, 3, 5, SignatureGuidancePhaseKind.Full, 0.35f)]
        [TestCase(4d, 3, 5, SignatureGuidancePhaseKind.Full, 0.28f)]
        [TestCase(7d, 3, 5, SignatureGuidancePhaseKind.Full, 0.07f)]
        [TestCase(8d, 3, 5, SignatureGuidancePhaseKind.Hidden, 0f)]
        public void Calculate_UsesTheDefinedPhaseTable(double successfulSignatures, int fullDisplayAfter,
            int fadeOutCount, SignatureGuidancePhaseKind expectedKind, float expectedAlpha) {
            SignatureGuidancePhase result = SignatureGuidancePhaseCalculator.Calculate(successfulSignatures,
                fullDisplayAfter, fadeOutCount);

            Assert.That(result.Kind, Is.EqualTo(expectedKind));
            Assert.That(result.Alpha, Is.EqualTo(expectedAlpha).Within(0.0001f));
        }

        [Test]
        public void Calculate_ZeroThresholdsHideImmediately() {
            SignatureGuidancePhase result = SignatureGuidancePhaseCalculator.Calculate(0d, 0, 0);

            Assert.That(result.Kind, Is.EqualTo(SignatureGuidancePhaseKind.Hidden));
            Assert.That(result.Alpha, Is.Zero);
        }

        [Test]
        public void Calculate_DoesNotOverflowThresholdSum() {
            SignatureGuidancePhase atStartOfFade = SignatureGuidancePhaseCalculator.Calculate(
                int.MaxValue, int.MaxValue, int.MaxValue);
            SignatureGuidancePhase afterFade = SignatureGuidancePhaseCalculator.Calculate(
                (double)int.MaxValue + int.MaxValue, int.MaxValue, int.MaxValue);

            Assert.That(atStartOfFade.Kind, Is.EqualTo(SignatureGuidancePhaseKind.Full));
            Assert.That(atStartOfFade.Alpha, Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(afterFade.Kind, Is.EqualTo(SignatureGuidancePhaseKind.Hidden));
        }
    }
}
