using System;
using System.Collections.Generic;
using Types.Upgrades;

namespace Clicker.Editor.Upgrades {
    internal static class UpgradeTreeSelection {
        private static readonly List<UpgradeNodeDefinition> _selectedNodes = new();

        public static event Action SelectionChanged;

        public static IReadOnlyList<UpgradeNodeDefinition> SelectedNodes {
            get {
                PruneMissingNodes();
                return _selectedNodes;
            }
        }

        public static void SetSelection(UpgradeNodeDefinition node) {
            _selectedNodes.Clear();
            if (node != null) {
                _selectedNodes.Add(node);
            }

            SelectionChanged?.Invoke();
        }

        public static void SetSelection(IEnumerable<UpgradeNodeDefinition> nodes) {
            _selectedNodes.Clear();
            if (nodes != null) {
                foreach (var node in nodes) {
                    if (node != null && !_selectedNodes.Contains(node)) {
                        _selectedNodes.Add(node);
                    }
                }
            }

            SelectionChanged?.Invoke();
        }

        public static void ToggleNode(UpgradeNodeDefinition node) {
            if (node == null) return;

            if (_selectedNodes.Contains(node)) {
                _selectedNodes.Remove(node);
            }
            else {
                _selectedNodes.Add(node);
            }

            SelectionChanged?.Invoke();
        }

        public static bool Contains(UpgradeNodeDefinition node) {
            PruneMissingNodes();
            return node != null && _selectedNodes.Contains(node);
        }

        private static void PruneMissingNodes() {
            for (var i = _selectedNodes.Count - 1; i >= 0; i--) {
                if (_selectedNodes[i] == null) {
                    _selectedNodes.RemoveAt(i);
                }
            }
        }
    }
}
