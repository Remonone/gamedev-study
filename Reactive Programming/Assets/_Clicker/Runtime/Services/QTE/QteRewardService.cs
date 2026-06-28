using Services.Player;
using Services.Statistics;
using Types.Enums;
using Types.Modifiers;
using Types.QTE;
using Types.Values;

namespace Services.QTE {
    public class QteRewardService : IService {
        
        private readonly StatisticsService _statisticsService;
        private readonly Storage _storage;
        
        public QteRewardService(StatisticsService statisticsService, Storage storage) {
            _statisticsService = statisticsService;
            _storage = storage;
        }

        public void GrantReward(GovernmentInteractionType type, QteRewardDefinition reward) {
            var wallet = _statisticsService.Get(StatisticKeys.PassiveResourceIncomePerSecond);
            var income = GetIncomeByType(wallet, type);
            var addedReward = income * reward.CurrentAmountMultiplier;
            if (addedReward <= Value.Zero) return;
            _storage.AddMoney(type, addedReward);
        }

        private Value GetIncomeByType(Wallet wallet, GovernmentInteractionType type) {
            switch (type) {
                case GovernmentInteractionType.MayorOffice:
                    return wallet.MayorWallet;
                case GovernmentInteractionType.FireFighterStation:
                    return wallet.FirefighterWallet;
                case GovernmentInteractionType.PoliceStation:
                    return wallet.PoliceWallet;
                case GovernmentInteractionType.Hospital:
                    return wallet.AmbulanceWallet;
                case GovernmentInteractionType.Court:
                    return wallet.CourtWallet;
                case GovernmentInteractionType.Archive:
                    return wallet.ArchiveWallet;
                default:
                    return new Value(0);
            }
        }
    }
}