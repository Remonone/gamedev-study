using System.Collections.Generic;
using Services.Components;
using Services.Components.Instances;
using R3;
using Types.Modifiers.Definitions;

namespace Services.Statistics.Trackers {
    public class MayorClicksTracker : StatisticTrackerBase {
        
        private readonly StructureClickService _structureClickService;
        private readonly CompositeDisposable _disposable = new();
        
        public MayorClicksTracker(IStatisticsWriter statistics) : base(statistics) {
            _structureClickService = ServiceLocator.Instance.GetService<StructureClickService>();
        }

        public override IReadOnlyCollection<string> ProducedStatisticIds { get; } = new[] {
            StatisticKeys.MayorClicks.Id
        };
        
        public override void Start() {
            _structureClickService.StructureInteraction
                .Where(interaction => GovernmentInteractionType.MayorOffice.Equals(interaction.GovernmentInteraction))
                .Subscribe(_ => Increment(StatisticKeys.MayorClicks)).AddTo(_disposable);
        }
        
        public override void Dispose() {
            _disposable.Dispose();
        }
    }
}