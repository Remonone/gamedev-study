using UnityEngine;

namespace Presentation {
    public sealed class ResearchOfferPresentationModel {
        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        public string Rarity { get; }
        public Color RarityColor { get; }
        public Sprite Icon { get; }

        public ResearchOfferPresentationModel(
            string id, string title, string description, string rarity, Color rarityColor, Sprite icon) {
            Id = id;
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Rarity = rarity ?? string.Empty;
            RarityColor = rarityColor;
            Icon = icon;
        }
    }

    public sealed class ActivePracticePresentationModel {
        public string Title { get; }
        public string Description { get; }
        public string Duration { get; }
        public Sprite Icon { get; }

        public ActivePracticePresentationModel(string title, string description, string duration, Sprite icon) {
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            Duration = duration ?? string.Empty;
            Icon = icon;
        }
    }
}
