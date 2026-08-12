using System;
using R3;

namespace Services {
    public enum NormalDocumentProcessingSource {
        Manual = 0,
        Office = 1
    }

    public readonly struct AcceptedNormalDocument {
        public NormalDocumentProcessingSource Source { get; }
        public int SelectedQuality { get; }
        public float ProcessingQuality { get; }

        public AcceptedNormalDocument(
            NormalDocumentProcessingSource source,
            int selectedQuality,
            float processingQuality = 1f) {
            Source = source;
            SelectedQuality = Math.Clamp(selectedQuality, 1, 10);
            ProcessingQuality = !float.IsNaN(processingQuality) && !float.IsInfinity(processingQuality)
                ? Math.Clamp(processingQuality, 0f, 1f)
                : 0f;
        }
    }

    public sealed class AcceptedNormalDocumentService : IService {
        private readonly Subject<AcceptedNormalDocument> _processed = new();

        public Observable<AcceptedNormalDocument> Processed => _processed;

        internal void Report(NormalDocumentProcessingSource source, int selectedQuality) {
            Report(source, selectedQuality, 1f);
        }

        internal void Report(
            NormalDocumentProcessingSource source,
            int selectedQuality,
            float processingQuality) {
            _processed.OnNext(new AcceptedNormalDocument(source, selectedQuality, processingQuality));
        }

        public void Dispose() {
            _processed.Dispose();
        }
    }
}
