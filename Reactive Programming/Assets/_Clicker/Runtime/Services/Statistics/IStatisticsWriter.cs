using System;

namespace Services.Statistics {
    public interface IStatisticsWriter {
        void Set<T>(StatisticKey<T> key, T value);
        void Update<T>(StatisticKey<T> key, Func<T, T> updater);
        
        void Increment(StatisticKey<int> key);
        void Add(StatisticKey<int> key, int value);
        void Add(StatisticKey<float> key, float value);
        void Add(StatisticKey<double> key, double value);
    }
}