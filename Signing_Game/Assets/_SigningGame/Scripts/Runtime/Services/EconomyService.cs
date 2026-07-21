using Data.Economy;

namespace Services {
    public class EconomyService : IService {
        private Wallet _wallet;

        public EconomyService() {
            _wallet = new Wallet();
            
        }
        

        public void Dispose() {
            _wallet?.Dispose();
        }
    }
}