using Data.Input;

namespace Contracts {
    public interface ISignatureRecorder {
        bool IsAttemptActive { get; }
        bool IsStrokeActive { get; }

        void BeginAttempt(float startTime);

        void BeginStroke(SignatureInputPoint firstPoint);

        void AddPoint(SignatureInputPoint point);

        void EndStroke(SignatureInputPoint finalPoint);

        SignatureAttempt CompleteAttempt(float endTime);

        void CancelAttempt();
    }
}