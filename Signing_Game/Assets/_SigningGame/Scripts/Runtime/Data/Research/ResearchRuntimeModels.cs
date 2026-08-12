using System;

namespace Data.Research {
    public readonly struct PendingPracticeState {
        public string PracticeId { get; }
        public float FrozenSignatureThreshold { get; }

        public PendingPracticeState(string practiceId, float frozenSignatureThreshold) {
            PracticeId = practiceId;
            FrozenSignatureThreshold = frozenSignatureThreshold;
        }
    }

    public sealed class ActivePracticeState {
        public PracticeDefinition Definition { get; }
        public float Effectiveness { get; }
        public bool IsPermanent { get; }
        public double RemainingSeconds { get; internal set; }

        public ActivePracticeState(
            PracticeDefinition definition,
            float effectiveness,
            bool isPermanent,
            double remainingSeconds) {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Effectiveness = effectiveness;
            IsPermanent = isPermanent;
            RemainingSeconds = remainingSeconds;
        }
    }
}
