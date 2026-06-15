using System;
using Newtonsoft.Json.Linq;

namespace Services.Statistics {
    public interface IStatisticsItem : IDisposable {
        string Id { get; }
        object StoredValue { get; }
        System.Type StoredValueType { get; }
        bool IsPersistent { get; }

        JToken SaveValue();
        void LoadValue(JToken data);
    }
}