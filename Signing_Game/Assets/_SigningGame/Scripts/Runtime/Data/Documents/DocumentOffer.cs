using System;
using UnityEngine;
using Utils;

namespace Data.Documents {
    public readonly struct DocumentOfferKey : IEquatable<DocumentOfferKey> {
        public DocumentKind Kind { get; }
        public string DomainId { get; }

        public DocumentOfferKey(DocumentKind kind, string domainId) {
            if (string.IsNullOrWhiteSpace(domainId)) throw new ArgumentException("A document offer ID is required.", nameof(domainId));
            Kind = kind;
            DomainId = domainId;
        }

        public bool Equals(DocumentOfferKey other) {
            return Kind == other.Kind && string.Equals(DomainId, other.DomainId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is DocumentOfferKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Kind, DomainId);
        public static bool operator ==(DocumentOfferKey left, DocumentOfferKey right) => left.Equals(right);
        public static bool operator !=(DocumentOfferKey left, DocumentOfferKey right) => !left.Equals(right);
    }

    public sealed class DocumentOffer : IEquatable<DocumentOffer> {
        public DocumentOfferKey Key { get; }
        public bool IsAvailable { get; }
        public string Header { get; }
        public Sprite Icon { get; }
        public string PersonName { get; }
        public int? PersonAge { get; }
        public Value? Amount { get; }
        public double? InternalMultiplier { get; }
        public bool RequiresStamp { get; }

        public DocumentOffer(
            DocumentOfferKey key,
            bool isAvailable,
            string header = null,
            Sprite icon = null,
            string personName = null,
            int? personAge = null,
            Value? amount = null,
            double? internalMultiplier = null,
            bool requiresStamp = false) {
            Key = key;
            IsAvailable = isAvailable;
            Header = header;
            Icon = icon;
            PersonName = personName;
            PersonAge = personAge;
            Amount = amount;
            InternalMultiplier = internalMultiplier;
            RequiresStamp = requiresStamp;
        }

        public bool Equals(DocumentOffer other) {
            return other != null && Key == other.Key && IsAvailable == other.IsAvailable &&
                   string.Equals(Header, other.Header, StringComparison.Ordinal) && Icon == other.Icon &&
                   string.Equals(PersonName, other.PersonName, StringComparison.Ordinal) &&
                   PersonAge == other.PersonAge && Nullable.Equals(Amount, other.Amount) &&
                   InternalMultiplier == other.InternalMultiplier && RequiresStamp == other.RequiresStamp;
        }

        public override bool Equals(object obj) => Equals(obj as DocumentOffer);

        public override int GetHashCode() {
            var hash = new HashCode();
            hash.Add(Key);
            hash.Add(IsAvailable);
            hash.Add(Header, StringComparer.Ordinal);
            hash.Add(Icon);
            hash.Add(PersonName, StringComparer.Ordinal);
            hash.Add(PersonAge);
            hash.Add(Amount);
            hash.Add(InternalMultiplier);
            hash.Add(RequiresStamp);
            return hash.ToHashCode();
        }
    }
}
