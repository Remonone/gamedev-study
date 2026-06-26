using System;
using System.Collections.Generic;
using R3;
using Types.Events.Global;
using UnityEngine;

namespace Services.Events {
    public class GlobalEventService : IService, IStartable, IDisposable {
        private readonly CompositeDisposable _disposable = new();

        private readonly IReadOnlyList<GlobalEvent> _events;
        private readonly InvalidationService _invalidationService;
        private readonly float _intervalSeconds;
        private readonly ReactiveProperty<GlobalEvent> _currentEvent = new(null);

        private float _cooldownRemainingSeconds;
        private float _activeRemainingSeconds;

        public ReadOnlyReactiveProperty<GlobalEvent> CurrentEvent => _currentEvent;

        public GlobalEvent ActiveEvent => _currentEvent.Value;

        public GlobalEventService(IReadOnlyList<GlobalEvent> events, InvalidationService invalidationService,
            float intervalMinutes) {
            _events = events;
            _invalidationService = invalidationService;
            _intervalSeconds = Mathf.Max(0f, intervalMinutes) * 60f;
            _cooldownRemainingSeconds = _intervalSeconds;
        }
        
        public void StartService() {
            Observable.EveryUpdate()
                .Subscribe(_ => Tick(Time.deltaTime))
                .AddTo(_disposable);
        }

        private void Tick(float deltaTime) {
            if (_events == null || _events.Count == 0) {
                return;
            }

            if (_currentEvent.Value != null) {
                _activeRemainingSeconds -= deltaTime;
                if (_activeRemainingSeconds <= 0f) {
                    EndCurrentEvent();
                }
                return;
            }

            _cooldownRemainingSeconds -= deltaTime;
            if (_cooldownRemainingSeconds <= 0f) {
                StartRandomEvent();
            }
        }

        private void StartRandomEvent() {
            var selectedEvent = SelectRandomEvent();
            if (selectedEvent == null || selectedEvent.DurationSeconds <= 0f) {
                _cooldownRemainingSeconds = _intervalSeconds;
                return;
            }

            _activeRemainingSeconds = selectedEvent.DurationSeconds;
            _currentEvent.Value = selectedEvent;
            InvokeStarted(selectedEvent);
            _invalidationService.InvalidateAll();
        }

        private GlobalEvent SelectRandomEvent() {
            for (var attempt = 0; attempt < _events.Count; attempt++) {
                var selectedEvent = _events[UnityEngine.Random.Range(0, _events.Count)];
                if (selectedEvent != null) {
                    return selectedEvent;
                }
            }

            return null;
        }

        private void EndCurrentEvent() {
            var endedEvent = _currentEvent.Value;
            if (endedEvent == null) return;

            InvokeEnded(endedEvent);
            _currentEvent.Value = null;
            _activeRemainingSeconds = 0f;
            _cooldownRemainingSeconds = _intervalSeconds;
            _invalidationService.InvalidateAll();
        }

        private static void InvokeStarted(GlobalEvent globalEvent) {
            if (globalEvent.Effects == null) return;

            foreach (var effect in globalEvent.Effects) {
                effect?.OnStarted();
            }
        }

        private static void InvokeEnded(GlobalEvent globalEvent) {
            if (globalEvent.Effects == null) return;

            foreach (var effect in globalEvent.Effects) {
                effect?.OnEnded();
            }
        }

        public void Dispose() {
            EndCurrentEvent();
            _disposable.Dispose();
            _currentEvent.Dispose();
        }
    }
}
