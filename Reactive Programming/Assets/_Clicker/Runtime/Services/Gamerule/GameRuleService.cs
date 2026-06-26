using System;
using System.Collections.Generic;
using R3;

namespace Services.Gamerule {
    public class GameRuleService : IService {
        private Dictionary<string, object> _gamerules = new();

        private Subject<GameRulePair> _onGameRuleChanged = new();
        
        public Observable<GameRulePair> OnGameRuleChanged => _onGameRuleChanged;

        public GameRuleService(IReadOnlyDictionary<string, object> gamerules) {
            _gamerules = new Dictionary<string, object>(gamerules);
            foreach (var (name, value) in gamerules) {
                _onGameRuleChanged.OnNext(new GameRulePair { Name = name, Value = value });
            }
        }
        
        public void RegisterGameRule(string name, object value) {
            if (_gamerules.ContainsKey(name)) {
                throw new ArgumentException($"Gamerule '{name}' already registered.");
            }
            _gamerules.Add(name, value);
            _onGameRuleChanged.OnNext(new GameRulePair { Name = name, Value = value });
        }

        public void SetGameRule(string name, object value) {
            if (!_gamerules.ContainsKey(name)) {
                throw new ArgumentException($"Gamerule '{name}' not registered.");
            }
            _gamerules[name] = value;
            _onGameRuleChanged.OnNext(new GameRulePair { Name = name, Value = value });
        }

        public object GetGameRule(string name) {
            return _gamerules.TryGetValue(name, out var value) ? value : null;
        }

        public struct GameRulePair {
            public string Name;
            public object Value;
        }
    }
}