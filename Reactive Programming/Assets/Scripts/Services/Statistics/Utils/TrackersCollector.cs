using System.Collections.Generic;
using Services.Statistics.Trackers;

namespace Services.Statistics {
    public static class TrackersCollector {

        public static List<IStatisticTracker> Collect(IStatisticsWriter statistics) {
            return new() {
                new ClickTracker(statistics),
                new MayorClicksTracker(statistics)
            };
        }
    }
}