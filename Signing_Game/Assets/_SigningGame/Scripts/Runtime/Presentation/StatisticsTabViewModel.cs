using System;
using System.Collections.Generic;
using Constants;
using Data.Statistics;
using R3;
using Services;

namespace Presentation {
    /// <summary>
    /// Builds the statistics tab contents from the designer layout and mirrors live
    /// values out of <see cref="GameStatisticsService"/>. Rows are created once from the
    /// config; Refresh only updates formatted values.
    /// </summary>
    public sealed class StatisticsTabViewModel : IDisposable {
        private readonly GameStatisticsService _statistics;
        private readonly List<StatisticsCategoryPresentationModel> _categories = new();
        private readonly Subject<Unit> _changed = new();
        private readonly IDisposable _statisticsSubscription;

        public StatisticsTabViewModel(
            StatisticsTabLayoutDefinition layout,
            GameStatisticsService statistics) {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            _statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));

            IReadOnlyList<StatisticsTabCategory> categories = layout.Categories ?? Array.Empty<StatisticsTabCategory>();
            for (int categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++) {
                StatisticsTabCategory category = categories[categoryIndex];
                if (category == null) continue;
                var rows = new List<StatisticRowPresentationModel>();
                IReadOnlyList<StatisticsTabTracker> trackers = category.Trackers;
                if (trackers == null) continue;
                for (int trackerIndex = 0; trackerIndex < trackers.Count; trackerIndex++) {
                    StatisticsTabTracker tracker = trackers[trackerIndex];
                    if (tracker == null || string.IsNullOrWhiteSpace(tracker.StatisticId)) continue;
                    string statisticId = tracker.StatisticId;
                    rows.Add(new StatisticRowPresentationModel(
                        statisticId,
                        tracker.DisplayName,
                        ReadFormattedValue(statisticId)));
                }

                _categories.Add(new StatisticsCategoryPresentationModel(category.Title, rows));
            }

            _statisticsSubscription = _statistics.Changed.Subscribe(_ => _changed.OnNext(Unit.Default));
        }

        public Observable<Unit> Changed => _changed;

        public IReadOnlyList<StatisticsCategoryPresentationModel> Categories => _categories;

        /// <summary>
        /// Re-reads every tracked statistic. Returns true when any formatted value changed.
        /// </summary>
        public bool Refresh() {
            bool changed = false;
            for (int categoryIndex = 0; categoryIndex < _categories.Count; categoryIndex++) {
                IReadOnlyList<StatisticRowPresentationModel> rows = _categories[categoryIndex].Rows;
                for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++) {
                    StatisticRowPresentationModel row = rows[rowIndex];
                    string value = ReadFormattedValue(row.StatisticId);
                    if (string.Equals(value, row.Value, StringComparison.Ordinal)) continue;
                    row.Value = value;
                    changed = true;
                }
            }

            return changed;
        }

        private string ReadFormattedValue(string statisticId) {
            return _statistics.TryGetValue(statisticId, out double value)
                ? GameStatisticFormats.Format(value, GameStatisticFormats.Resolve(statisticId))
                : "0";
        }

        public void Dispose() {
            _statisticsSubscription.Dispose();
            _changed.Dispose();
        }
    }
}
