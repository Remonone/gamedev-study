using System.Collections.Generic;
using UnityEngine;
using Utils;

namespace Presentation {
    public enum BillTab {
        Catalog,
        Active,
        Completed
    }

    public enum BillCardKind {
        Catalog,
        Active,
        Completed
    }

    public sealed class BillRequirementPresentationModel {
        public string Label { get; }
        public string Tooltip { get; }
        public Color Color { get; }
        public bool IsSatisfied { get; }

        public BillRequirementPresentationModel(string label, string tooltip, Color color, bool isSatisfied) {
            Label = label;
            Tooltip = tooltip;
            Color = color;
            IsSatisfied = isSatisfied;
        }
    }

    public sealed class BillStatisticPresentationModel {
        public string Label { get; }
        public string Value { get; }
        public string Tooltip { get; }

        public BillStatisticPresentationModel(string label, string value, string tooltip = null) {
            Label = label;
            Value = value;
            Tooltip = tooltip ?? string.Empty;
        }
    }

    public sealed class BillCardPresentationModel {
        public BillCardKind Kind { get; }
        public long Id { get; }
        public string Title { get; }
        public string Description { get; }
        public Sprite Icon { get; }
        public IReadOnlyList<BillRequirementPresentationModel> Requirements { get; }
        public Value Price { get; }
        public bool CanPurchase { get; }
        public string PurchaseBlockerTooltip { get; }
        public float Progress { get; }
        public string ProgressText { get; }
        public int Priority { get; }
        public int MaximumPriority { get; }
        public bool IsExpanded { get; }
        public IReadOnlyList<BillStatisticPresentationModel> Statistics { get; }

        public BillCardPresentationModel(
            BillCardKind kind,
            long id,
            string title,
            string description,
            Sprite icon,
            IReadOnlyList<BillRequirementPresentationModel> requirements = null,
            Value price = default,
            bool canPurchase = false,
            string purchaseBlockerTooltip = null,
            float progress = 0f,
            string progressText = null,
            int priority = 1,
            int maximumPriority = 1,
            bool isExpanded = false,
            IReadOnlyList<BillStatisticPresentationModel> statistics = null) {
            Kind = kind;
            Id = id;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Icon = icon;
            Requirements = requirements ?? System.Array.Empty<BillRequirementPresentationModel>();
            Price = price;
            CanPurchase = canPurchase;
            PurchaseBlockerTooltip = purchaseBlockerTooltip ?? string.Empty;
            Progress = progress;
            ProgressText = progressText ?? string.Empty;
            Priority = priority;
            MaximumPriority = maximumPriority;
            IsExpanded = isExpanded;
            Statistics = statistics ?? System.Array.Empty<BillStatisticPresentationModel>();
        }
    }
}
