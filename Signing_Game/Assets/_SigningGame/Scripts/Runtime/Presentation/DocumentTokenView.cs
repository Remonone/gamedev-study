using R3;
using Services;
using Services.Locator;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation {
    public class DocumentTokenView : MonoBehaviour {
        [SerializeField] private TMP_Text _quantity;
        [SerializeField] private Image _progressBar;

        private DocumentTokenViewModel _viewModel;
        
        private void Start() {
            _quantity.text = "0";
            _progressBar.fillAmount = 0;

            var generator = ServiceLocator.For(this).Get<DocumentGeneratorService>();
            _viewModel = new DocumentTokenViewModel(generator);
            
            _viewModel.ProgressChanged.Subscribe(progress => _progressBar.fillAmount = progress).AddTo(this);
            _viewModel.QuantityChanged.Subscribe(quantity => _quantity.text = quantity.ToString()).AddTo(this);
        }

        private void OnDestroy() {
            _viewModel.Dispose();
        }
    }
}