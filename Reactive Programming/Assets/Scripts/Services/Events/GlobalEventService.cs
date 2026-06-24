using System;
using System.Collections.Generic;
using R3;
using Types.Events.Global;

namespace Services.Events {
    public class GlobalEventService : IService, IStartable, IDisposable {
        
        private CompositeDisposable _disposable = new();

        private readonly IReadOnlyList<GlobalEvent> _events;

        public GlobalEventService(IReadOnlyList<GlobalEvent> events) {
            _events = events;
        }
        
        public void StartService() {
            
        }


        public void Dispose() {
            _disposable.Dispose();
        }
    }
}