using R3;

namespace Services.Statistics {
    public interface IStatisticsReader {
        T Get<T>(StatisticKey<T> key);
        Observable<T> GetObservable<T>(StatisticKey<T> key);
        bool IsRegistered<T>(StatisticKey<T> key);
    }
}