using System;
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
        private readonly QteModifierAggregator _modifierAggregator;
        private readonly Random _rng = new();
        
        public QteRewardService(StatisticsService statisticsService, Storage storage, QteModifierAggregator modifierAggregator) {
            _statisticsService = statisticsService;
            _storage = storage;
            _modifierAggregator = modifierAggregator;
        }

        public void GrantPlayerClickReward(GovernmentInteractionType type, QteRewardDefinition reward) {
            GrantReward(type, reward, 1f);
        }

        public void GrantWorkerClickReward(GovernmentInteractionType type, QteRewardDefinition reward, float workerIncomeMultiplier) {
            GrantReward(type, reward, workerIncomeMultiplier);
        }

        private void GrantReward(GovernmentInteractionType type, QteRewardDefinition reward, float workerIncomeMultiplier) {
            if (reward == null) return;

            var wallet = _statisticsService.Get(StatisticKeys.PassiveResourceIncomePerSecond);
            var income = GetIncomeByType(wallet, type);
            var addedReward = income * reward.CurrentAmountMultiplier;
            addedReward *= Math.Max(0d, _modifierAggregator.ResolveIncomeClickMultiplier());

            var critChance = _modifierAggregator.ResolveIncomeClickCritChance();
            if (critChance > 0f && _rng.NextDouble() < critChance) {
                addedReward *= _modifierAggregator.ResolveIncomeClickCritMultiplier();
            }

            addedReward *= Math.Max(0d, workerIncomeMultiplier);
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
