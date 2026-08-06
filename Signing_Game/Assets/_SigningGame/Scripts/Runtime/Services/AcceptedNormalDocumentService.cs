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

        public AcceptedNormalDocument(NormalDocumentProcessingSource source, int selectedQuality) {
            Source = source;
            SelectedQuality = Math.Clamp(selectedQuality, 1, 10);
        }
    }

    public sealed class AcceptedNormalDocumentService : IService {
        private readonly Subject<AcceptedNormalDocument> _processed = new();

        public Observable<AcceptedNormalDocument> Processed => _processed;

        internal void Report(NormalDocumentProcessingSource source, int selectedQuality) {
            _processed.OnNext(new AcceptedNormalDocument(source, selectedQuality));
        }

        public void Dispose() {
            _processed.Dispose();
        }
    }
}
