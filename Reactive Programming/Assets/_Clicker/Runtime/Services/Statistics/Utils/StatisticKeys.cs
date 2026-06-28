using Types.Modifiers;

namespace Services.Statistics {
    public static class StatisticKeys {
        
        public static StatisticKey<int> MayorClicks = new("MayorClicks");
        public static StatisticKey<int> TotalClicks = new("TotalClicks");
        public static StatisticKey<Wallet> PassiveResourceIncomePerSecond = new("PassiveResourceIncomePerSecond");
    }
}
