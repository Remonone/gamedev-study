namespace Services.Statistics {
    public static class StatisticsRegistry {
        public static void RegisterStatistics(StatisticsService statisticsService) {
            statisticsService.Register(StatisticKeys.MayorClicks);
            statisticsService.Register(StatisticKeys.TotalClicks);
        }
    }
}