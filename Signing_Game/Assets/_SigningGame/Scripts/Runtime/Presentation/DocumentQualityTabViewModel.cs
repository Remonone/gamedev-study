using System;
using R3;
using Services;

namespace Presentation {
    public sealed class DocumentQualityTabViewModel : IDisposable {
        private readonly DocumentQualityService _quality;

        public Observable<Unit> Changed => _quality.Changed;
        public int SelectedQualityLevel => _quality.SelectedQualityLevel;
        public int MaximumQualityLevel => _quality.MaximumQualityLevel;
        public bool IsAvailable => _quality.IsAvailable;

        public DocumentQualityTabViewModel(DocumentQualityService quality) {
            _quality = quality ?? throw new ArgumentNullException(nameof(quality));
        }

        public void Decrease() => _quality.MoveSelection(-1);
        public void Increase() => _quality.MoveSelection(1);

        public void Dispose() { }
    }
}
