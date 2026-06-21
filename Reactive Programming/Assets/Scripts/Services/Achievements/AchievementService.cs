using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using R3;
using Save;
using Services.Statistics;
using Types.Modifiers.Definitions.Achievements;

namespace Services.Achievements {
    public class AchievementService : IService, ISaveable, IDisposable, IStartable {

        private readonly IStatisticsReader _statistics;
        private readonly IReadOnlyList<IAchievement> _achievements;
        private readonly Dictionary<string, IAchievement> _achievementsById;
        private readonly ReplaySubject<IAchievement> _unlocked = new();
        private readonly CompositeDisposable _disposable = new();

        private HashSet<string> _restoredCompleted = new();
        
        public ReplaySubject<IAchievement> Unlocked => _unlocked;
        public IReadOnlyList<IAchievement> Achievements => _achievements;
        
        public AchievementService(IReadOnlyList<IAchievement> achievements) {
            _achievements = achievements;
            _achievementsById = achievements.ToDictionary(achievement => achievement.Id);

            ValidateUniqueIds();
        }

        public void StartService() {
            foreach(var achievement in _achievements) {
                if (_restoredCompleted.Contains(achievement.Id)) {
                    achievement.RestoreCompleted();
                    _unlocked.OnNext(achievement);
                    continue;
                }
                
                achievement.IsCompleted.Where(completed => completed)
                    .Where(_ => !achievement.IsLoadedAsCompleted)
                    .Take(1)
                    .Subscribe(_ => _unlocked.OnNext(achievement))
                    .AddTo(_disposable);
                
                achievement.Start();
            }
        }

        public bool IsCompleted(string id) =>
            _achievementsById.TryGetValue(id, out var a) && a.IsCompleted.CurrentValue;

        private void ValidateUniqueIds() {
            var ids = new HashSet<string>();
            foreach (var achievement in _achievements) {
                if(!ids.Add(achievement.Id))
                    throw new InvalidOperationException($"Achievement with id '{achievement.Id}' already exists.");
            }
        }

        public string SaveKey => "Achievements";
        public int Priority => 80;

        public JToken Save() {
            var completed = _achievements
                .Where(a => a.IsCompleted.CurrentValue)
                .Select(a => a.Id);
            return new JObject {
                ["completed"] = new JArray(completed)
            };
        }

        public void Load(JToken data) {
            _restoredCompleted = (data?["completed"] as JArray)?
                .Select(t => t.Value<string>())
                .ToHashSet() ?? new HashSet<string>();
        }
        
        public void Dispose() {
            _disposable.Dispose();
            _unlocked.Dispose();
            foreach(var achievement in _achievements) achievement.Dispose();
        }
    }
}