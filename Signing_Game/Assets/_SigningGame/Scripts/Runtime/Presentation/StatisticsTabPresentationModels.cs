using System.Collections.Generic;

namespace Presentation {
    public sealed class StatisticRowPresentationModel {
        public string StatisticId { get; }
        public string Label { get; }

        /// <summary>Formatted display value; updated in place by the view model refresh.</summary>
        public string Value { get; internal set; }

        public StatisticRowPresentationModel(string statisticId, string label, string value) {
            StatisticId = statisticId;
            Label = label;
            Value = value;
        }
    }

    public sealed class StatisticsCategoryPresentationModel {
        public string Title { get; }
        public IReadOnlyList<StatisticRowPresentationModel> Rows { get; }

        public StatisticsCategoryPresentationModel(string title, IReadOnlyList<StatisticRowPresentationModel> rows) {
            Title = title;
            Rows = rows;
        }
    }
}
