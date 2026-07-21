using System;
using R3;
using Services;
using UnityEngine;
using Utils;

namespace Data.Economy {
    public class Wallet : IDisposable {

        private Value _balance;
        private Subject<IValue> _balanceChanged = new();
        
        public Observable<IValue> BalanceChanged => _balanceChanged;

        public Wallet() {
            _balance = new Value(0);
        }

        public void ReplenishWallet(Value value) {
            if (value.IsZero || !_balance.IsSignificant(value)) return;
            _balance += value;
            _balanceChanged.OnNext(_balance);
        }

        public bool TryWithdrawWalle(Value value) {
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
