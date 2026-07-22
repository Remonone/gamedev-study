using R3;
using Utils;

namespace Services {
    public class WalletService : IService {
        private Value _balance;
        private Subject<IValue> _balanceChanged = new();
        
        public Observable<IValue> BalanceChanged => _balanceChanged;

        public WalletService() {
            _balance = new(0);
        }
        
        public void ReplenishWallet(Value value) {
            if (value.IsZero || !_balance.IsSignificant(value)) return;
            _balance += value;
            _balanceChanged.OnNext(_balance);
        }

        public bool TryWithdrawWallet(Value value) {
            if (value.IsZero) return false;
            if (!_balance.IsSignificant(value)) return true;
            if (_balance < value) return false;
            _balance -= value;
            _balanceChanged.OnNext(_balance);
            return true;
        }
        

        public void Dispose() {
            _balanceChanged?.Dispose();
        }
    }
}