using NUnit.Framework;
using UI;

namespace Tests.EditMode {
    public sealed class PullTabViewTests {
        [TestCase(false, 99f, 100f, false)]
        [TestCase(false, 100f, 100f, true)]
        [TestCase(false, 101f, 100f, true)]
        [TestCase(true, 99f, 100f, true)]
        [TestCase(true, 100f, 100f, false)]
        [TestCase(true, 101f, 100f, false)]
        public void ResolveOpenState_WithThreshold_ChangesOnlyAtOrBeyondThreshold(
            bool startedOpen,
            float distance,
            float threshold,
            bool expectedOpen) {
            bool result = PullTabView.ResolveOpenState(startedOpen, true, distance, threshold);

            Assert.That(result, Is.EqualTo(expectedOpen));
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void ResolveOpenState_WithoutThreshold_AlwaysChangesState(
            bool startedOpen,
            bool expectedOpen) {
            bool result = PullTabView.ResolveOpenState(startedOpen, false, 0f, 100f);

            Assert.That(result, Is.EqualTo(expectedOpen));
        }
    }
}
