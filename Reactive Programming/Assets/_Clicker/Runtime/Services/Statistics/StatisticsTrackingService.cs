using System;
using System.Collections.Generic;
using System.Linq;
using Services.Statistics.Trackers;

namespace Services.Statistics {
    public class StatisticsTrackingService : IService, IDisposable {

        private readonly IReadOnlyList<IStatisticTracker> _trackers;
        private bool _started;

        public StatisticsTrackingService(IEnumerable<IStatisticTracker> trackers) {
            _trackers = trackers.ToList();
            
            ValidateUniqueTrackers(_trackers);
            ValidateUniqueProducedStatistics(_trackers);
        }

        public void Start() {
            if (_started) {
                throw new InvalidOperationException("Statistics tracking service is already started.");
            }
            
            foreach (var tracker in _trackers) {
                tracker.Start();
            }
            
            _started = true;
        }
        
        private static void ValidateUniqueTrackers(IReadOnlyList<IStatisticTracker> trackers) {
            var trackerTypes = new HashSet<Type>();

            foreach (var tracker in trackers) {
                var type = tracker.GetType();

                if (!trackerTypes.Add(type)) {
                    throw new InvalidOperationException(
                        $"Statistic tracker '{type.Name}' is registered more than once.");
                }
            }
        }

        private static void ValidateUniqueProducedStatistics(IReadOnlyList<IStatisticTracker> trackers) {
            var owners = new Dictionary<string, IStatisticTracker>();

            foreach (var tracker in trackers) {
                foreach (var statisticId in tracker.ProducedStatisticIds) {
                    if (owners.TryGetValue(statisticId, out var existingOwner)) {
                        throw new InvalidOperationException(
                            $"Statistic '{statisticId}' is produced by both " +
                            $"'{existingOwner.GetType().Name}' and '{tracker.GetType().Name}'.");
                    }

                    owners.Add(statisticId, tracker);
                }
            }
        }
        
        public void Dispose() {
            foreach (var tracker in _trackers) {
                tracker.Dispose();
            }
        }
    }
}