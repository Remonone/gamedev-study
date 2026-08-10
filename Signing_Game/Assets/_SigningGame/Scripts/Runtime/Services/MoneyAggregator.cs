using Contracts;
using Cysharp.Threading.Tasks;
using Services.Locator;
using R3;
using Utils;

namespace Services {
    public readonly struct MoneyTransaction {
        public Value Requested { get; }
        public Value Credited { get; }

        public MoneyTransaction(Value requested, Value credited) {
            Requested = requested;
            Credited = credited;
        }
    }

    public class MoneyAggregator : IService, IInitialize, IMoneyAggregator {

        private PlayerStatStash _stash;
        private WalletService _wallet;
        private readonly Subject<MoneyTransaction> _moneyAdded = new();

        public Observable<MoneyTransaction> MoneyAdded => _moneyAdded;
        
        public void Dispose() {
            _moneyAdded.Dispose();
        }

        public Value AddMoney(Value amount) {
            var value = amount * _stash.GetIncomeModifiers();
            Value before = _wallet.CurrentBalance;
            if (!_wallet.ReplenishWallet(value)) return Value.Zero;

            Value after = _wallet.CurrentBalance;
            Value credited = after > before ? (after - before).Value : Value.Zero;
            if (!credited.IsZero) _moneyAdded.OnNext(new MoneyTransaction(value, credited));
            return credited;
        }

        public UniTask InitializeAsync(IServiceScope scope) {
            _stash = scope.Get<PlayerStatStash>();
            _wallet = scope.Get<WalletService>();
            return UniTask.CompletedTask;
        }
    }
}
