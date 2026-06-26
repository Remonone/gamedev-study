using System;
using System.Collections.Generic;
using System.Linq;
using Economy.Providers;
using Newtonsoft.Json.Linq;
using R3;
using Save;
using Types;
using Types.Enums;
using Types.Practices;
using UnityEngine;

namespace Services {
    public class RecycleService : IService, ISaveable, IStartable, IDisposable {
        private const string _DefaultBuildingChoice = "__default_clicks";
        
        private readonly List<PracticeRecycleModifier> _recycleModifiers = new();
        private readonly PracticeService _practiceService;
        private readonly PracticeRewardConfig _rewardConfig;
        private readonly BuildingWatcherService _buildingWatcherService;
        private readonly System.Random _random = new();
        
        
        private CompositeDisposable _disposable = new();
        
        public IReadOnlyList<PracticeRecycleModifier> RecycleModifiers => _recycleModifiers;
        
        public RecycleService(
            PracticeService practiceService, 
            BuildingWatcherService buildingWatcherService,
            PracticeRewardConfig rewardConfig) {
            _practiceService = practiceService;
            _buildingWatcherService = buildingWatcherService;
            _rewardConfig = rewardConfig;
        }
        
        public void StartService() {
            _practiceService.OnRecycleRarity.Subscribe(ApplyRecycleBuff).AddTo(_disposable);
        }
        
        private void ApplyRecycleBuff(PracticeRarity rarity) {
            var targetName = PickRecycleTargetName();
            var stat = PickRecycleStat(targetName == _DefaultBuildingChoice);
            var power = _rewardConfig != null ? _rewardConfig.GetRecyclePower(rarity) : 0.05f;
            var modifier = CreateRecycleModifier(targetName, stat, power);
            _recycleModifiers.Add(modifier);
        }

        private string PickRecycleTargetName() {
            var buildings = _buildingWatcherService.BuildingsByName.Values.ToList();
            var roll = _random.Next(buildings.Count);
            var building = buildings[roll];
            if(!building.Definition.IsUpgradeable) return _DefaultBuildingChoice;
            return building.Definition.Name;
        }

        private StatType PickRecycleStat(bool isDefaultClickTarget) {
            if (isDefaultClickTarget) {
                return StatType.ClickIncome;
            }

            var stats = new[] {
                StatType.Income,
                StatType.Frequency,
                StatType.MultiplierCoefficient,
                StatType.CriticalChance,
                StatType.CriticalMultiplier,
                StatType.ClickIncome
            };
            return stats[_random.Next(stats.Length)];
        }

        private PracticeRecycleModifier CreateRecycleModifier(string targetName, StatType stat, float power) {
            var value = ConvertRecyclePower(stat, power);
            return new PracticeRecycleModifier {
                TargetBuildingName = targetName,
                AppliesToAllClicks = targetName == _DefaultBuildingChoice,
                Stat = stat,
                Operation = ModifierOp.AddPercent,
                Value = value,
                ModifierId = $"practice_recycle_{Guid.NewGuid():N}"
            };
        }

        private float ConvertRecyclePower(StatType stat, float power) {
            power = Mathf.Max(0f, power);
            return stat switch {
                StatType.CriticalChance => Mathf.Min(0.5f, power * 0.5f),
                StatType.Frequency => power * 0.75f,
                StatType.CriticalMultiplier => power,
                StatType.MultiplierCoefficient => power,
                StatType.ClickIncome => power,
                _ => power
            };
        }
        
        private JObject SaveRecycleModifier(PracticeRecycleModifier modifier) {
            return new JObject(
                new JProperty("TargetBuildingName", modifier.TargetBuildingName),
                new JProperty("AppliesToAllClicks", modifier.AppliesToAllClicks),
                new JProperty("Stat", modifier.Stat.ToString()),
                new JProperty("Operation", modifier.Operation.ToString()),
                new JProperty("Value", modifier.Value),
                new JProperty("Priority", modifier.Priority),
                new JProperty("ModifierId", modifier.ModifierId));
        }
        
        private bool TryLoadRecycleModifier(JToken token, out PracticeRecycleModifier modifier) {
            modifier = new PracticeRecycleModifier();
            if (token is not JObject obj) {
                return false;
            }

            if (!Enum.TryParse(obj.Value<string>("Stat"), out StatType stat)) {
                return false;
            }

            if (!Enum.TryParse(obj.Value<string>("Operation"), out ModifierOp operation)) {
                return false;
            }

            modifier.TargetBuildingName = obj.Value<string>("TargetBuildingName");
            modifier.AppliesToAllClicks = obj.Value<bool?>("AppliesToAllClicks") ?? false;
            modifier.Stat = stat;
            modifier.Operation = operation;
            modifier.Value = obj.Value<float?>("Value") ?? 0f;
            modifier.Priority = obj.Value<int?>("Priority") ?? 0;
            modifier.ModifierId = obj.Value<string>("ModifierId");
            return true;
        }

        public void Dispose() {
            _disposable.Dispose();
        }

        public string SaveKey => "Recycled";
        public int Priority => 60;
        public JToken Save() {
            return new JObject {
                new JProperty("RecycleModifiers", new JArray(_recycleModifiers.Select(SaveRecycleModifier)))
            };
        }

        public void Load(JToken data) {
            _recycleModifiers.Clear();
            foreach (var token in data["RecycleModifiers"] ?? new JArray()) {
                if (TryLoadRecycleModifier(token, out var modifier)) {
                    _recycleModifiers.Add(modifier);
                }
            }
        }

        
    }
}