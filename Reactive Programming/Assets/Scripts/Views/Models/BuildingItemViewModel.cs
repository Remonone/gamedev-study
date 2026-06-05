using System;
using Bases.Buildings;
using Components;
using Player;
using R3;
using Services;
using Types;
using Types.Economy;

namespace Views.Models {
    public class BuildingItemViewModel : IDisposable {

        private string _name;
        private StructureType _type;
        private CompositeDisposable _disposable = new();

        private float _amount;
        
        private EconomyService _economyService;
        
        public ReactiveProperty<float> Income = new();
        public ReactiveProperty<float> Frequency = new();
        public ReactiveProperty<float> Cost = new();
        public ReactiveProperty<float> Stability = new();
        public ReactiveProperty<float> StabilityMultiplier = new();
        public ReactiveProperty<float> Multiplier = new();
        public ReactiveProperty<float> CriticalChance = new();
        public ReactiveProperty<float> CriticalMultiplier = new();
        public ReactiveProperty<bool> CanPurchase = new();
        
        public StructureType Type => _type;
        public string Name => _name;
        
        public BuildingItemViewModel(BuildingDefinition definition) {
            _name = definition.Name;
            _type = definition.Type;
            
            _economyService = ServiceLocator.Instance.GetService<EconomyService>();
            var storage = ServiceLocator.Instance.GetService<Storage>();
            
            _economyService.BuildingUpdate
                .Where(update => update.Building.Definition.Equals(definition))
                .Select(update => update.Stats)
                .Subscribe(OnValuesUpdated)
                .AddTo(_disposable);
            storage[_type].Subscribe(amount => {
                _amount = amount;
                CanPurchase.Value = amount >= Cost.Value;
            });
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
            CanPurchase.Value = _amount >= Cost.Value;
        }

        public void Upgrade() {
            _economyService.PurchaseBuilding(_name);
        }

        public void Dispose() {
            _disposable.Dispose();
        }
    }
}