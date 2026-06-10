using System;
using System.Collections.Generic;
using System.Linq;
using Components;
using R3;
using Services;

namespace Views.Models {
    public class UpgradesTabViewModel : IDisposable {
        private readonly UpgradeService _upgradeService;
        private readonly List<UpgradeTileViewModel> _tiles = new();
        private readonly Dictionary<string, UpgradeTileViewModel> _tilesById = new();

        public IReadOnlyList<UpgradeTileViewModel> Tiles => _tiles;
        public ReactiveProperty<string> SelectedNodeId = new(string.Empty);
        public ReactiveProperty<UpgradeTileViewModel> SelectedTile = new(null);

        public UpgradesTabViewModel() {
            _upgradeService = ServiceLocator.Instance.GetService<UpgradeService>();
        }

        public void RequestInitialState() {
            if (_tiles.Count > 0) {
                return;
            }

            foreach (var state in _upgradeService.GetAllUpgradeStates()) {
                var tile = new UpgradeTileViewModel(state, _upgradeService);
                _tiles.Add(tile);
                _tilesById.Add(tile.Id, tile);
            }
        }

        public void SelectNode(string nodeId) {
            SelectedNodeId.Value = nodeId;
            SelectedTile.Value = !string.IsNullOrWhiteSpace(nodeId) && _tilesById.TryGetValue(nodeId, out var tile)
                ? tile
                : null;
        }

        public void UpgradeSelectedNode() {
            SelectedTile.Value?.Upgrade();
        }

        public void Dispose() {
            foreach (var tile in _tiles.ToArray()) {
                tile.Dispose();
            }
        }
    }
}
