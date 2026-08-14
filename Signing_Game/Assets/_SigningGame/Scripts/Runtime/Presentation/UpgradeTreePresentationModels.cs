using UnityEngine;

namespace Presentation {
    public sealed class UpgradeNodePresentationModel {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public Sprite Icon { get; }
        public Vector2 Position { get; }
        public int CurrentLevel { get; }
        public int MaxLevel { get; }
        public string LevelText { get; }
        public string Price { get; }
        public bool IsUnlocked { get; }
        public bool IsVisible { get; }
        public bool IsPending { get; }
        public float Effectiveness { get; }
        public bool CanPurchase { get; }

        public UpgradeNodePresentationModel(string id, string name, string description, Sprite icon,
            Vector2 position, int currentLevel, int maxLevel, string levelText, string price, bool isUnlocked, bool isVisible,
            bool isPending, float effectiveness, bool canPurchase) {
            Id = id;
            Name = name;
            Description = description;
            Icon = icon;
            Position = position;
            CurrentLevel = currentLevel;
            MaxLevel = maxLevel;
            LevelText = levelText;
            Price = price;
            IsUnlocked = isUnlocked;
            IsVisible = isVisible;
            IsPending = isPending;
            Effectiveness = effectiveness;
            CanPurchase = canPurchase;
        }

        public static Vector2 ToRuntimePosition(Vector2 treePosition) {
            // Editor graph coordinates use GUI Y-down; RectTransform anchored positions use Y-up.
            return new Vector2(treePosition.x, -treePosition.y);
        }
    }

    public sealed class UpgradeEdgePresentationModel {
        public string ParentId { get; }
        public string ChildId { get; }
        public Vector2 Start { get; }
        public Vector2 End { get; }

        public UpgradeEdgePresentationModel(string parentId, string childId, Vector2 start, Vector2 end) {
            ParentId = parentId;
            ChildId = childId;
            Start = start;
            End = end;
        }
    }
}
