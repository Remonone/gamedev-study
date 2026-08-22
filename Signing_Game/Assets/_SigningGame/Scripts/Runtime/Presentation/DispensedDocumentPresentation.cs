using System;
using System.Globalization;
using Data.Documents;
using UnityEngine;

namespace Presentation {
    public sealed class DispensedDocumentPresentation {
        public DocumentOffer Offer { get; }
        public DocumentOfferKey Key => Offer.Key;
        public DocumentKind Kind => Key.Kind;
        public bool IsAvailable => Offer.IsAvailable;
        public int ProducerRegistrationIndex { get; }
        public long Revision { get; }
        public ulong TextSeed { get; }
        public Color HeaderColor { get; }
        public string HeaderText { get; }
        public Sprite HeaderIcon => Offer.Icon;
        public string ProfileText { get; }
        public string AmountText { get; }
        public string InternalMultiplierText { get; }
        public bool RequiresStamp => Offer.RequiresStamp;

        public DispensedDocumentPresentation(
            DocumentOffer offer,
            int producerRegistrationIndex,
            long revision,
            ulong textSeed,
            Color headerColor) {
            Offer = offer ?? throw new ArgumentNullException(nameof(offer));
            ProducerRegistrationIndex = producerRegistrationIndex;
            Revision = revision;
            TextSeed = textSeed;
            HeaderColor = headerColor;

            HeaderText = offer.Key.Kind switch {
                DocumentKind.Upgrade => offer.Header ?? string.Empty,
                DocumentKind.ClerkHire => "CLERK HIRE",
                DocumentKind.ClerkSalaryReview => "SALARY REVIEW",
                DocumentKind.Bill => offer.Header ?? "BILL",
                DocumentKind.Practice => offer.Header ?? "PRACTICE",
                DocumentKind.SignatureGuidance => offer.Header ?? "SIGNATURE GUIDANCE",
                _ => string.Empty
            };
            ProfileText = string.IsNullOrWhiteSpace(offer.PersonName)
                ? string.Empty
                : offer.PersonAge.HasValue
                    ? $"{offer.PersonName}, {offer.PersonAge.Value}"
                    : offer.PersonName;
            AmountText = offer.Amount.HasValue
                ? offer.Key.Kind switch {
                    DocumentKind.ClerkHire => $"Bid: {offer.Amount.Value}",
                    DocumentKind.Bill => $"Bill cost: {offer.Amount.Value}",
                    DocumentKind.Practice => $"Practice value: {offer.Amount.Value}",
                    _ => $"Review cost: {offer.Amount.Value}"
                }
                : string.Empty;
            InternalMultiplierText = offer.InternalMultiplier.HasValue
                ? $"Internal multiplier: x{offer.InternalMultiplier.Value.ToString("0.##", CultureInfo.InvariantCulture)}"
                : string.Empty;
        }
    }
}
