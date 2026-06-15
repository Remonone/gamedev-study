using System;
using Newtonsoft.Json.Linq;
using R3;

namespace Services.Statistics {
    internal sealed class StatisticsItem<T> : IStatisticsItem {
        
        private readonly ReactiveProperty<T> _value;
        
        public string Id { get; }
        
        public bool IsPersistent { get; }

        public T Value => _value.Value;
        
        public object StoredValue => _value.Value;

        public Type StoredValueType => typeof(T);

        public Observable<T> Changed => _value;
        
        public StatisticsItem(string id, T initialValue) {
            Id = id;
            _value = new ReactiveProperty<T>(initialValue);
        }
        
        public void SetValue(T value) {
            _value.Value = value;
        }

        public JToken SaveValue() {
            return JToken.FromObject(_value.Value);
        }

        public void LoadValue(JToken token) {
            if (token == null || token.Type == JTokenType.Null) {
                return;
            }
            T value = token.ToObject<T>();
            _value.Value = value;
        }

        public void Dispose() {
            _value.Dispose();
        }
    }
}