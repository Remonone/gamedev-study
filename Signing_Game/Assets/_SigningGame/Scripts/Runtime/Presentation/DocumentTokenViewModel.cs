using System;
using R3;
using Services;

namespace Presentation {
    public class DocumentTokenViewModel : IDisposable {
        
        private DocumentGeneratorService _generator;
        private CompositeDisposable _disposable = new();
        
        private ReactiveProperty<int> _quantityChanged = new();
        private ReactiveProperty<float> _progressChanged = new();
        
        public Observable<int> QuantityChanged => _quantityChanged;
        public Observable<float> ProgressChanged => _progressChanged;
        
        public DocumentTokenViewModel(DocumentGeneratorService service) {
            _generator = service;
            
            _generator.DocumentCount.Subscribe(UpdateQuantity).AddTo(_disposable);
            _generator.CurrentProgress.Subscribe(UpdateProgress).AddTo(_disposable);
        }
        
        private void UpdateQuantity(int quantity) {
            _quantityChanged.Value = quantity;
        }

        private void UpdateProgress(float progress) {
            _progressChanged.Value = progress;
        }

        public void Dispose() {
            _disposable.Dispose();
            _quantityChanged.Dispose();
            _progressChanged.Dispose();
        }
    }
}
