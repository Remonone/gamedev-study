using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using R3;
using Save;

namespace Services.Statistics {
    public class StatisticsService : IService, IStatisticsReader, IStatisticsWriter, ISaveable, IDisposable {
        
        private Dictionary<string, IStatisticsItem> _statisticsItems = new();

        public void Register<T>(StatisticKey<T> key, T initialValue = default, bool isPersistent = true) {
            ValidateKey(key.Id);
            
            if (_statisticsItems.ContainsKey(key.Id)) {
                throw new ArgumentException($"Statistic '{key.Id}' already registered.");
            }
            _statisticsItems.Add(key.Id, new StatisticsItem<T>(key.Id, initialValue, isPersistent));
        }

        public bool IsRegistered<T>(StatisticKey<T> key) {
            return _statisticsItems.TryGetValue(key.Id, out var item) 
                   && item.StoredValueType == typeof(T);
        }

        public T Get<T>(StatisticKey<T> key) {
            return GetRequiredItem(key).Value;
        }

        public Observable<T> GetObservable<T>(StatisticKey<T> key) {
            return GetRequiredItem(key).Changed;
        }
        
        void IStatisticsWriter.Set<T>(StatisticKey<T> key, T value) {
            GetRequiredItem(key).SetValue(value);
        }

        void IStatisticsWriter.Update<T>(StatisticKey<T> key, Func<T, T> updater) {
            if (updater == null) throw new ArgumentNullException(nameof(updater));
            
            var item = GetRequiredItem(key);
            item.SetValue(updater(item.Value));
        }
        
        void IStatisticsWriter.Increment(StatisticKey<int> key) {
            ((IStatisticsWriter) this).Update(key, value => value + 1);
        }

        void IStatisticsWriter.Add(StatisticKey<int> key, int amount) {
            ((IStatisticsWriter) this).Update(key, current => current + amount);
        }
        
        void IStatisticsWriter.Add(StatisticKey<float> key, float amount) {
            ((IStatisticsWriter) this).Update(key, current => current + amount);
        }
        
        void IStatisticsWriter.Add(StatisticKey<double> key, double amount) {
            ((IStatisticsWriter) this).Update(key, current => current + amount);
        }
        
        private static void ValidateKey(string id) {
            if (string.IsNullOrWhiteSpace(id)) {
                throw new ArgumentException("Statistic key cannot be empty.", nameof(id));
            }
        }

        private StatisticsItem<T> GetRequiredItem<T>(StatisticKey<T> key) {
            ValidateKey(key.Id);

            if (!_statisticsItems.TryGetValue(key.Id, out var item)) {
                throw new KeyNotFoundException($"Statistic '{key.Id}' is not registered.");
            }

            if (item is not StatisticsItem<T> typedItem) {
                throw new InvalidOperationException(
                    $"Statistic '{key.Id}' has type '{item.StoredValueType.Name}' but got '{typeof(T).Name}'");
            }
            return typedItem;
        }
        
        public void Dispose() {
            foreach (var item in _statisticsItems.Values) {
                item.Dispose();
            }
            _statisticsItems.Clear();
        }

        public string SaveKey => "Statistics";
        public int Priority => 50;
        public JToken Save() {
            var result = new JObject();
            var values = new JObject();
            
            foreach (var item in _statisticsItems.Values) {
                if (!item.IsPersistent) continue;
                values[item.Id] = item.SaveValue();
            }
            result["values"] = values;
            return result;
        }

        public void Load(JToken data) {
            var values = data["values"] as JObject;
            if (values == null)
                return;
            foreach (var item in values.Properties()) {
                var id = item.Name;
                if (!_statisticsItems.TryGetValue(id, out var statisticItem))
                    continue;
                if (!statisticItem.IsPersistent) continue;

                try {
                    statisticItem.LoadValue(item.Value);
                } catch (Exception ex) {
                  throw new InvalidOperationException($"Failed to load statistic '{id}' as type '{statisticItem.StoredValueType.Name}'", ex);  
                }
            }
        }
    }
}
