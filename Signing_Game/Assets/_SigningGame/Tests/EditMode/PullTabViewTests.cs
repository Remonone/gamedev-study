using NUnit.Framework;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;

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

        [Test]
        public void ResolvePulledAnchoredAxis_AlwaysUsesStableClosedBaseline() {
            Assert.That(PullTabView.ResolvePulledAnchoredAxis(900f, 100f, 600f), Is.EqualTo(400f));
            Assert.That(PullTabView.ResolvePulledAnchoredAxis(900f, 600f, 600f), Is.EqualTo(900f));
        }

        [Test]
        public void PointerTooltipTrigger_HidesWhenDisabledAndUnbound() {
            var gameObject = new GameObject("TooltipTrigger");
            try {
                var trigger = gameObject.AddComponent<PointerTooltipTrigger>();
                int shown = 0;
                int hidden = 0;
                trigger.Bind((_, _) => shown++, () => hidden++);
                trigger.OnPointerEnter(new PointerEventData(null));
                Assert.That(shown, Is.EqualTo(1));

                typeof(PointerTooltipTrigger)
                    .GetMethod("OnDisable", System.Reflection.BindingFlags.Instance |
                                            System.Reflection.BindingFlags.NonPublic)
                    ?.Invoke(trigger, null);
                Assert.That(hidden, Is.EqualTo(1));
                trigger.Unbind();
                Assert.That(hidden, Is.EqualTo(2));
            } finally {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
