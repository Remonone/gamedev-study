using System;
using System.Collections.Generic;
using R3;
using Services;
using Types.Economy.Cost;
using Types.Upgrades;
using UnityEngine;

namespace Views.Models {
    public enum UpgradeNodeVisualState {
        Locked,
        Available,
        InProgress,
        Completed
    }

    public class UpgradeTileViewModel : IDisposable {
        private readonly UpgradeService _upgradeService;
        private readonly CompositeDisposable _disposable = new();

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public Sprite Icon { get; }
        public Vector2 Position { get; }
        public int MaxLevel { get; }
        public IReadOnlyList<string> ChildIds { get; }

        public ReactiveProperty<UpgradeNodeVisualState> State = new(UpgradeNodeVisualState.Locked);
        public ReactiveProperty<int> Level = new(0);
        public ReactiveProperty<Price> Price = new(new Price());
        public ReactiveProperty<bool> CanUpgrade = new(false);

        public UpgradeTileViewModel(UpgradeNodeState initialState, UpgradeService upgradeService) {
            _upgradeService = upgradeService;

            var definition = initialState.Definition;
            Id = definition.Id;
            Name = definition.Name;
            Description = definition.Description;
            Icon = definition.Icon;
            Position = definition.Position;
            MaxLevel = Mathf.Max(1, definition.MaxLevel);
            ChildIds = definition.ChildIds != null
                ? definition.ChildIds
                : (IReadOnlyList<string>)Array.Empty<string>();

            _upgradeService.ObserveUpgradeState(Id)
                .Subscribe(OnStateChanged)
                .AddTo(_disposable);

            _upgradeService.AffordabilityChanged
                .Subscribe(_ => RefreshCanUpgrade())
                .AddTo(_disposable);
        }

        public void Upgrade() {
            _upgradeService.TryUpgrade(Id);
        }

        public void Dispose() {
            _disposable.Dispose();
        }

        private void OnStateChanged(UpgradeNodeState state) {
            if (state == null) {
                return;
            }

            Level.Value = state.Level;
            State.Value = ToVisualState(state.CurrentState);
            Price.Value = _upgradeService.GetUpgradePrice(Id);
            RefreshCanUpgrade();
        }

        private void RefreshCanUpgrade() {
            CanUpgrade.Value = _upgradeService.CanUpgrade(Id);
        }

        private static UpgradeNodeVisualState ToVisualState(UpgradeNodeState.State state) {
            return state switch {
                UpgradeNodeState.State.Locked => UpgradeNodeVisualState.Locked,
                UpgradeNodeState.State.Available => UpgradeNodeVisualState.Available,
                UpgradeNodeState.State.InProgress => UpgradeNodeVisualState.InProgress,
                UpgradeNodeState.State.Completed => UpgradeNodeVisualState.Completed,
                _ => UpgradeNodeVisualState.Locked
            };
        }
    }
}
