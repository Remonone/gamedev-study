namespace Services.Statistics {
    public record StatisticKey<T>(string Id) {
        public override string ToString() => Id;
    }
}