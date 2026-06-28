using System.Collections.Generic;
using System.Linq;
using R3;
using Services.Components;
using Types.Buildings;
using Utils;

namespace Services.Statistics.Trackers {
    public class ResourceIncomePerSecondTracker : StatisticTrackerBase {
        private readonly EconomyService _economyService;
        private readonly BuildingWatcherService _buildingWatcherService;
        private readonly StateBenefitCalculationService _stateBenefitCalculationService;
        private IReadOnlyList<BuildingState> _states;
        private readonly CompositeDisposable _disposable = new();
        private bool _isRefreshing;

        public ResourceIncomePerSecondTracker(IStatisticsWriter statistics) : base(statistics) {
            _economyService = ServiceLocator.Instance.GetService<EconomyService>();
            _buildingWatcherService = ServiceLocator.Instance.GetService<BuildingWatcherService>();
            _stateBenefitCalculationService = ServiceLocator.Instance.GetService<StateBenefitCalculationService>();
        }

        public override IReadOnlyCollection<string> ProducedStatisticIds { get; } = new[] {
            StatisticKeys.PassiveResourceIncomePerSecond.Id
        };

        public override void Start() {
            _economyService.BuildingUpdate
                .Subscribe(_ => Refresh())
                .AddTo(_disposable);
            Refresh();
        }

        private void Refresh() {
            if (_isRefreshing) return;

            _isRefreshing = true;
            try {
                var incomePerSecond = ResourceIncomePerSecondCalculator.Calculate(
                    _buildingWatcherService.BuildingsByName.Values.Where(building => building.Definition.IsUpgradeable),
                    _economyService,
                    _stateBenefitCalculationService);
                Set(StatisticKeys.PassiveResourceIncomePerSecond, incomePerSecond);
            }
            finally {
                _isRefreshing = false;
            }
        }

        public override void Dispose() {
            _disposable.Dispose();
        }
    }
}
