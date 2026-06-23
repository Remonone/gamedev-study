using System;
using Types.Modifiers.Definitions.Buildings;
using Services.Components;
using Services.Player;
using R3;
using Services;
using Types.Modifiers.Definitions;
using Types.Modifiers.Definitions.Cost;
using Types.Modifiers.Definitions.Values;
using UnityEngine;

namespace Views.Models {
    public class BuildingItemViewModel : IDisposable {
        private const int DefaultPurchaseLevels = 1;

        private string _name;
        private GovernmentInteractionType _type;
        private CompositeDisposable _disposable = new();
        
        private EconomyService _economyService;
        
        public ReactiveProperty<Value> Income = new(Value.Zero);
        public ReactiveProperty<float> Frequency = new();
        public ReactiveProperty<Price> Cost = new();
        public ReactiveProperty<float> Stability = new();
        public ReactiveProperty<float> StabilityMultiplier = new();
        public ReactiveProperty<float> Multiplier = new();
        public ReactiveProperty<float> CriticalChance = new();
        public ReactiveProperty<float> CriticalMultiplier = new();
        public ReactiveProperty<bool> CanPurchase = new();
        public ReactiveProperty<int> PurchaseLevels = new(DefaultPurchaseLevels);
        public ReactiveProperty<string> Description = new(string.Empty);
        public ReactiveProperty<Sprite> Icon = new(null);

        private Storage _storage;
        
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
            _storage.StructureMoney.Subscribe(_ => RefreshPurchaseState()).AddTo(_disposable);
            PurchaseLevels.Subscribe(_ => RefreshPurchaseState()).AddTo(_disposable);
        }

        private void OnValuesUpdated(ComputedStats stats) {
            Income.Value = stats.Income;
            Frequency.Value = stats.Frequency;
            Stability.Value = stats.StabilityModifier;
            StabilityMultiplier.Value = stats.StabilityModifierMultiplier;
            Multiplier.Value = stats.MultiplierCoefficient;
            CriticalChance.Value = stats.CriticalChance;
            CriticalMultiplier.Value = stats.CriticalMultiplier;
            RefreshPurchaseState();
        }

        private void RefreshPurchaseState() {
            var levels = PurchaseLevels.Value;
            Cost.Value = _economyService.GetBuildingPurchasePrice(_name, levels);
            CanPurchase.Value = _economyService.CanPurchaseBuilding(_name, levels);
        }

        public void SetPurchaseLevels(int levels) {
            if (levels <= 0) {
                levels = DefaultPurchaseLevels;
            }

            if (PurchaseLevels.Value == levels) {
                return;
            }

            PurchaseLevels.Value = levels;
        }

        public void Upgrade() {
            _economyService.PurchaseBuilding(_name, PurchaseLevels.Value);
            RefreshPurchaseState();
        }

        public void Dispose() {
            _disposable.Dispose();
        }
    }
}
