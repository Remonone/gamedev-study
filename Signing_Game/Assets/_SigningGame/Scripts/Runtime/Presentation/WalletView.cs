using System;
using R3;
using Services;
using Services.Locator;
using TMPro;
using UnityEngine;

namespace Presentation {
    public class WalletView : MonoBehaviour {
        
        [SerializeField] private TextMeshProUGUI _balanceText;
        
        private WalletViewModel _viewModel;

        private void Start() {
            var walletService = ServiceLocator.For(this).Get<WalletService>();
            var viewModel = new WalletViewModel(walletService);
            _viewModel = viewModel;
            _viewModel.Balance.Subscribe(OnBalanceChanged).AddTo(this);
        }

        private void OnBalanceChanged(string balance) {
            _balanceText.text = $"{balance}$";
        }

        private void OnDestroy() {
            _viewModel.Dispose();
        }
    }
}