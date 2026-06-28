using System;
using System.Collections.Generic;
using Services;
using Types.Buildings;
using Types.Enums;
using Types.Modifiers;
using Types.Values;

namespace Utils {
    public static class ResourceIncomePerSecondCalculator {
        public static Wallet Calculate(
            IEnumerable<BuildingState> buildings,
            EconomyService economyService,
            StateBenefitCalculationService stateBenefitCalculationService) {
            var result = new Wallet();

            foreach (var building in buildings) {
                if (building.Level <= 0) continue;

                var stats = economyService.ComputeStatsForBuilding(building);
                var incomePerTick = stats.Income;
                stateBenefitCalculationService.CalculateBenefits(building, ref incomePerTick);

                var frequency = Math.Max(0.001f, stats.Frequency);
                AddIncome(ref result, building.Definition.Type, incomePerTick * frequency);
            }

            return result;
        }

        private static void AddIncome(ref Wallet wallet, GovernmentInteractionType type, Value amount) {
            switch (type) {
                case GovernmentInteractionType.MayorOffice:
                    wallet.MayorWallet += amount;
                    break;
                case GovernmentInteractionType.Court:
                    wallet.CourtWallet += amount;
                    break;
                case GovernmentInteractionType.FireFighterStation:
                    wallet.FirefighterWallet += amount;
                    break;
                case GovernmentInteractionType.PoliceStation:
                    wallet.PoliceWallet += amount;
                    break;
                case GovernmentInteractionType.Hospital:
                    wallet.AmbulanceWallet += amount;
                    break;
                case GovernmentInteractionType.Archive:
                    wallet.ArchiveWallet += amount;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}
