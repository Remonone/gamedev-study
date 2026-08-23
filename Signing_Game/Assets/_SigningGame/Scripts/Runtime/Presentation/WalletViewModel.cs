using System;
using R3;
using Services;
using Utils;

namespace Presentation {
    public class WalletViewModel : IDisposable {

        public readonly ReactiveProperty<string> Balance;
        public Observable<Value> Credited { get; }

        private CompositeDisposable _disposable = new();

        public WalletViewModel(WalletService service) {
            if (service == null) throw new ArgumentNullException(nameof(service));
            Balance = new ReactiveProperty<string>();
            Credited = service.Credited;
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
