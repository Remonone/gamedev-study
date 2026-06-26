using System;
using R3;
using Services.Events;
using Types.Events.Global;

namespace Views.Models {
    public sealed class GlobalEventIndicatorViewModel : IDisposable {
        private readonly CompositeDisposable _disposable = new();

        public ReactiveProperty<GlobalEvent> CurrentEvent { get; } = new(null);

        public GlobalEventIndicatorViewModel(GlobalEventService globalEventService) {
            if (globalEventService == null) return;

            globalEventService.CurrentEvent
                .Subscribe(globalEvent => CurrentEvent.Value = globalEvent)
                .AddTo(_disposable);
        }

        public void Dispose() {
            _disposable.Dispose();
            CurrentEvent.Dispose();
        }
    }
}
