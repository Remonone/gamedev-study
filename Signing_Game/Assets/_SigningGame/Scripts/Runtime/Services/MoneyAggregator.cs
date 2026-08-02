using Contracts;
using Cysharp.Threading.Tasks;
using Services.Locator;
using Utils;

namespace Services {
    public class MoneyAggregator : IService, IInitialize, IMoneyAggregator {

        private PlayerStatStash _stash;
        private WalletService _wallet;
        
        public void Dispose() {
            
        }

        public Value AddMoney(Value amount) {
            var value = amount * _stash.GetIncomeModifiers();
            return _wallet.ReplenishWallet(value) ? value : Value.Zero;
        }

        public UniTask InitializeAsync(IServiceScope scope) {
            _stash = scope.Get<PlayerStatStash>();
            _wallet = scope.Get<WalletService>();
            return UniTask.CompletedTask;
        }
    }
}
