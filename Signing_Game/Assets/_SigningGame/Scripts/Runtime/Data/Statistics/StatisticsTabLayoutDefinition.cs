using System;
using System.Collections.Generic;
using UnityEngine;

namespace Data.Statistics {
    /// <summary>
    /// A single statistics tab entry: which statistic to show and under which display name.
    /// </summary>
    [Serializable]
    public sealed class StatisticsTabTracker {
        [SerializeField] private string _statisticId;
        [SerializeField] private string _displayName;

        public string StatisticId => _statisticId;
        public string DisplayName => _displayName;

        public StatisticsTabTracker() { }

        public StatisticsTabTracker(string statisticId, string displayName) {
            _statisticId = statisticId;
            _displayName = displayName;
        }
    }

    /// <summary>
    /// A named group of trackers rendered as one section inside the statistics tab.
    /// </summary>
    [Serializable]
    public sealed class StatisticsTabCategory {
        [SerializeField] private string _title;
        [SerializeField] private List<StatisticsTabTracker> _trackers = new();

        public string Title => _title;
        public IReadOnlyList<StatisticsTabTracker> Trackers => _trackers;

        public StatisticsTabCategory() { }

        public StatisticsTabCategory(string title, List<StatisticsTabTracker> trackers) {
            _title = title;
            _trackers = trackers;
        }
    }

    /// <summary>
    /// Designer-facing configuration of the statistics tab contents. Pure configuration:
    /// categories and trackers are authored here, values come from GameStatisticsService at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "Definitions/Statistics Tab Layout", fileName = "StatisticsTabLayout")]
    public sealed class StatisticsTabLayoutDefinition : ScriptableObject {
        [SerializeField] private List<StatisticsTabCategory> _categories = new();

        public IReadOnlyList<StatisticsTabCategory> Categories => _categories;

        /// <summary>Replaces the category list. Editor serialization in production; tests otherwise.</summary>
        internal void Initialize(List<StatisticsTabCategory> categories) {
            _categories = categories ?? new List<StatisticsTabCategory>();
        }
    }
}
