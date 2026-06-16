using System;
using Types.Enums.Buildings;
using Components;
using Services.Player;
using R3;
using Services;
using Types.Enums;
using Types.Enums.Cost;
using UnityEngine;

namespace Views.Models {
    public class BuildingItemViewModel : IDisposable {

        private string _name;
        private GovernmentInteractionType _type;
        private CompositeDisposable _disposable = new();
        
        private EconomyService _economyService;
        
        public ReactiveProperty<float> Income = new();
        public ReactiveProperty<float> Frequency = new();
        public ReactiveProperty<Price> Cost = new();
        public ReactiveProperty<float> Stability = new();
        public ReactiveProperty<float> StabilityMultiplier = new();
        public ReactiveProperty<float> Multiplier = new();
        public ReactiveProperty<float> CriticalChance = new();
        public ReactiveProperty<float> CriticalMultiplier = new();
        public ReactiveProperty<bool> CanPurchase = new();
        public ReactiveProperty<string> Description = new(string.Empty);
        public ReactiveProperty<Sprite> Icon = new(null);

        private Storage _storage;
        private ComputedStats _lastStats;
        
        public GovernmentInteractionType Type => _type;
        public string Name => _name;
        
        public BuildingItemViewModel(BuildingDefinition definition) {
            _name = definition.Name;
            _type = definition.Type;
            Description.Value = definition.Description;
            Icon.Value = definition.Icon;
            
            _economyService = ServiceLocator.Instance.GetService<EconomyService>();
            _storage = ServiceLocator.Instance.GetService<Storage>();
            
            
            
            _economyService.BuildingUpdate
                .Where(update => update.Building.Definition.Equals(definition))
                .Select(update => update.Stats)
                .Subscribe(OnValuesUpdated)
                .AddTo(_disposable);
            _storage.StructureMoney.Subscribe(_ => {
                CanPurchase.Value = CanBePurchased(_lastStats.Cost);
            }).AddTo(_disposable);
        }

        private void OnValuesUpdated(ComputedStats stats) {
            Income.Value = stats.Income;
            Frequency.Value = stats.Frequency;
            Cost.Value = stats.Cost;
            Stability.Value = stats.StabilityModifier;
            StabilityMultiplier.Value = stats.StabilityModifierMultiplier;
            Multiplier.Value = stats.MultiplierCoefficient;
            CriticalChance.Value = stats.CriticalChance;
            CriticalMultiplier.Value = stats.CriticalMultiplier;
            CanPurchase.Value = CanBePurchased(stats.Cost);
            _lastStats = stats;
        }

        private bool CanBePurchased(Price cost) {
            return _storage.CanAfford(cost);
        }

        public void Upgrade() {
            _economyService.PurchaseBuilding(_name);
        }

        public void Dispose() {
            _disposable.Dispose();
        }
    }
}
