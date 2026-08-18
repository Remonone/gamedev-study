using System;
using Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using R3;
using Utils;

namespace Services {
    public class WalletService : IService, ISaveable {
        private Value _balance;
        private readonly ReactiveProperty<IValue> _balanceChanged;
        private readonly Subject<Value> _credited = new();
        private readonly Subject<Value> _debited = new();

        public string SaveId => "wallet";
        
        public Observable<IValue> BalanceChanged => _balanceChanged;
        public Observable<Value> Credited => _credited;
        public Observable<Value> Debited => _debited;
        public Value CurrentBalance => _balance;

        public WalletService() {
            _balance = new(0);
            _balanceChanged = new ReactiveProperty<IValue>(_balance);
        }
        
        public bool ReplenishWallet(Value value) {
            return ReplenishWallet(value, true);
        }

        internal bool ReplenishWallet(Value value, bool notify) {
            if (value.IsZero || !_balance.IsSignificant(value)) return false;
            Value before = _balance;
            _balance += value;
            EmitCredited(before, _balance);
            if (notify) NotifyBalanceChanged();
            return true;
        }

        public bool CanAfford(Value value) {
            if (value.IsZero) return false;
            if (!_balance.IsSignificant(value)) return true;
            return _balance >= value;
        }

        public bool TryWithdrawWallet(Value value) {
            return TryWithdrawWallet(value, true);
        }

        internal bool TryWithdrawWallet(Value value, bool notify) {
            if (!CanAfford(value)) return false;
            if (!_balance.IsSignificant(value)) return true;
            Value before = _balance;
            _balance = (_balance - value).Value;
            EmitDebited(before, _balance);
            if (notify) NotifyBalanceChanged();
            return true;
        }

        private void EmitCredited(Value before, Value after) {
            Value? delta = after - before;
            if (delta.HasValue && !delta.Value.IsZero) _credited.OnNext(delta.Value);
        }

        private void EmitDebited(Value before, Value after) {
            Value? delta = before - after;
            if (delta.HasValue && !delta.Value.IsZero) _debited.OnNext(delta.Value);
        }

        internal void NotifyBalanceChanged() {
            _balanceChanged.Value = _balance;
        }

        public JToken Serialize() {
            return new JObject {
                ["stored"] = _balance.Stored,
                ["degree"] = _balance.Base.Degree
            };
        }

        public void Deserialize(JToken state) {
            if (state is not JObject data || !TryReadNumber(data["stored"], out double stored) ||
                data["degree"]?.Type != JTokenType.Integer) {
                throw new JsonSerializationException("Wallet save data is missing a numeric stored value or integer degree.");
            }

            int degree = data["degree"].Value<int>();
            bool invalidStored = double.IsNaN(stored) || double.IsInfinity(stored) || stored < 0d || stored >= 1000d;
            bool invalidDegree = degree < 0 || stored == 0d && degree != 0 || degree > 0 && stored < 1d;
            if (invalidStored || invalidDegree) {
                throw new JsonSerializationException("Wallet save data contains a negative, non-finite, or non-canonical value.");
            }

            var restoredBalance = new Value(stored, new BaseValue(degree));
            if (restoredBalance.Stored != stored || restoredBalance.Base.Degree != degree) {
                throw new JsonSerializationException("Wallet save data is not canonical.");
            }

            _balance = restoredBalance;
            _balanceChanged.Value = _balance;
        }
        

        public void Dispose() {
            _balanceChanged.Dispose();
            _credited.Dispose();
            _debited.Dispose();
        }

        private static bool TryReadNumber(JToken token, out double value) {
            if (token?.Type is JTokenType.Integer or JTokenType.Float) {
                value = token.Value<double>();
                return true;
            }

            value = default;
            return false;
        }
    }
}
