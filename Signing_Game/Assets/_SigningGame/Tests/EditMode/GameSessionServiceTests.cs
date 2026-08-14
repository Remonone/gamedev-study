using NUnit.Framework;
using Services;

namespace Tests.EditMode {
    public sealed class GameSessionServiceTests {
        [Test]
        public void LaunchMode_IsConsumedExactlyOnce() {
            var session = new GameSessionService();
            session.Prepare(GameLaunchMode.NewGame);

            Assert.That(session.TryConsume(out GameLaunchMode mode), Is.True);
            Assert.That(mode, Is.EqualTo(GameLaunchMode.NewGame));
            Assert.That(session.TryConsume(out _), Is.False);
        }

        [Test]
        public void Prepare_RejectsOverwrite_AndClearRemovesPendingMode() {
            var session = new GameSessionService();
            session.Prepare(GameLaunchMode.Continue);
            Assert.Throws<System.InvalidOperationException>(() => session.Prepare(GameLaunchMode.NewGame));
            session.ClearPending();
            Assert.That(session.TryConsume(out _), Is.False);
            Assert.DoesNotThrow(() => session.Prepare(GameLaunchMode.NewGame));
        }
    }
}
