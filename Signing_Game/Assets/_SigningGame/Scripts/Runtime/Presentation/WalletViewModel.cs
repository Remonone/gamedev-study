using System;
using R3;
using Services;
using Utils;

namespace Presentation {
    public class WalletViewModel : IDisposable {

        public readonly ReactiveProperty<string> Balance;

        private CompositeDisposable _disposable = new();

        public WalletViewModel(WalletService service) {
            Balance = new ReactiveProperty<string>();
            service.BalanceChanged.Subscribe(OnWalletChanged).AddTo(_disposable);
        }

        private void OnWalletChanged(IValue value) {
            Balance.Value = value.ToString();
        }
        
        public void Dispose() {
            _disposable.Dispose();
            Balance?.Dispose();
        }
    }
}
