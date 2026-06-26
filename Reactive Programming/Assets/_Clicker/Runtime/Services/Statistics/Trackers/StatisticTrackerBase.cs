using System;
using System.Collections.Generic;

namespace Services.Statistics.Trackers {
    public abstract class StatisticTrackerBase : IStatisticTracker {

        private readonly IStatisticsWriter _statistics;
        
        public abstract IReadOnlyCollection<string> ProducedStatisticIds { get; }
        
        protected StatisticTrackerBase(IStatisticsWriter statistics) {
            _statistics = statistics;
        }

        public abstract void Start();
        
        protected void Set<T>(StatisticKey<T> key, T value) => _statistics.Set(key, value);
        
        protected void Update<T>(StatisticKey<T> key, Func<T, T> updater) => _statistics.Update(key, updater);
        
        protected void Increment(StatisticKey<int> key) => _statistics.Increment(key);
        
        protected void Add(StatisticKey<int> key, int amount) => _statistics.Add(key, amount);
        
        protected void Add(StatisticKey<float> key, float amount) => _statistics.Add(key, amount);
        
        protected void Add(StatisticKey<double> key, double amount) => _statistics.Add(key, amount);
        
        public virtual void Dispose() {}
    }
}