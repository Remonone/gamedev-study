using NUnit.Framework;
using UI;
using Utils;

namespace Tests.EditMode {
    public sealed class IncomeNotificationQueueTests {
        [Test]
        public void Enqueue_EvictsOldestEntryAtCapacity() {
            var queue = new IncomeNotificationQueue();
            for (int value = 1; value <= IncomeNotificationQueue.Capacity + 1; value++) {
                queue.Enqueue(new Value(value), 0d);
            }

            Assert.That(queue.Count, Is.EqualTo(IncomeNotificationQueue.Capacity));
            Assert.That(queue.TryDequeueDue(0.1d, out Value first), Is.True);
            Assert.That(first, Is.EqualTo(new Value(2d)));
        }

        [Test]
        public void Dequeue_UsesInitialDelaySpacingAndNoCatchUpBursts() {
            var queue = new IncomeNotificationQueue();
            queue.Enqueue(new Value(1d), 0d);
            queue.Enqueue(new Value(2d), 0d);

            Assert.That(queue.TryDequeueDue(0.099d, out _), Is.False);
            Assert.That(queue.TryDequeueDue(0.1d, out Value first), Is.True);
            Assert.That(first, Is.EqualTo(new Value(1d)));
            Assert.That(queue.TryDequeueDue(0.199d, out _), Is.False);
            Assert.That(queue.TryDequeueDue(0.2d, out Value second), Is.True);
            Assert.That(second, Is.EqualTo(new Value(2d)));

            queue.Enqueue(new Value(3d), 10d);
            Assert.That(queue.TryDequeueDue(10.099d, out _), Is.False);
            Assert.That(queue.TryDequeueDue(10.1d, out Value afterIdle), Is.True);
            Assert.That(afterIdle, Is.EqualTo(new Value(3d)));

            queue.Enqueue(new Value(4d), 20d);
            queue.Enqueue(new Value(5d), 20d);
            Assert.That(queue.TryDequeueDue(25d, out Value afterHitch), Is.True);
            Assert.That(afterHitch, Is.EqualTo(new Value(4d)));
            Assert.That(queue.TryDequeueDue(25d, out _), Is.False);
        }
    }
}
