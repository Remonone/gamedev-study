using System;
using System.Collections.Generic;

namespace Services.Statistics.Trackers {
    public interface IStatisticTracker : IDisposable {
        IReadOnlyCollection<string> ProducedStatisticIds { get; }

        void Start();
    }
}