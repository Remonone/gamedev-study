using System.Collections.Generic;
using Services.Components;
using R3;

namespace Services.Statistics.Trackers {
    public class ClickTracker : StatisticTrackerBase {
        
        private readonly WorldCastService _worldCastService;
        
        private readonly CompositeDisposable _disposable = new();
        
        public ClickTracker(IStatisticsWriter statistics) : base(statistics) {
            _worldCastService = ServiceLocator.Instance.GetService<WorldCastService>();
        }

        public override IReadOnlyCollection<string> ProducedStatisticIds { get; } = new[] {
            StatisticKeys.TotalClicks.Id
        };
        
        public override void Start() {
                _worldCastService.OnClick
                    .Subscribe(_ => Increment(StatisticKeys.TotalClicks)).AddTo(_disposable);
        }
        
        public override void Dispose() {
            _disposable.Dispose();
        }
    }
}