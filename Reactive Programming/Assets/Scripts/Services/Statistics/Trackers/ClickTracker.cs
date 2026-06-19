using System.Collections.Generic;
using Services.Components;
using Services.Components.Instances;
using R3;

namespace Services.Statistics.Trackers {
    public class ClickTracker : StatisticTrackerBase {
        
        private readonly StructureClickService _structureClickService;
        
        private readonly CompositeDisposable _disposable = new();
        
        public ClickTracker(IStatisticsWriter statistics) : base(statistics) {
            _structureClickService = ServiceLocator.Instance.GetService<StructureClickService>();
        }

        public override IReadOnlyCollection<string> ProducedStatisticIds { get; } = new[] {
            StatisticKeys.TotalClicks.Id
        };
        
        public override void Start() {
                _structureClickService.StructureInteraction
                    .Subscribe(_ => Increment(StatisticKeys.TotalClicks)).AddTo(_disposable);
        }
        
        public override void Dispose() {
            _disposable.Dispose();
        }
    }
}