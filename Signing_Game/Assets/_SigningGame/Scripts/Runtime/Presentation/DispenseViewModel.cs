using System;
using System.Collections.Generic;
using Contracts;
using Data.Cache;
using Data.Documents;
using R3;
using UnityEngine;
using Utils.Text.Generator;

namespace Presentation {
    public sealed class DispenseViewModel : IDisposable {
        private readonly List<ProducerEntry> _producers = new();
        private readonly IReadOnlyCacheData<DocumentEntries> _documents;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly Subject<DispensedDocumentPresentation> _changed = new();
        private StableRandom _random;
        private DocumentOffer _currentOffer;
        private DispensedDocumentPresentation _current;
        private long _nextRevision;

        public DispensedDocumentPresentation Current => _current;
        public Observable<DispensedDocumentPresentation> Changed => _changed;

        public DispenseViewModel(
            IReadOnlyList<IDocumentProducer> producers,
            IReadOnlyCacheData<DocumentEntries> documents,
            StableRandom random) {
            if (producers == null) throw new ArgumentNullException(nameof(producers));
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _random = random;

            for (int index = 0; index < producers.Count; index++) {
                IDocumentProducer producer = producers[index]
                    ?? throw new ArgumentException("Document producer list contains null.", nameof(producers));
                _producers.Add(new ProducerEntry(producer, index));
            }

            _producers.Sort((left, right) => {
                int priority = right.Producer.Priority.CompareTo(left.Producer.Priority);
                return priority != 0 ? priority : left.RegistrationIndex.CompareTo(right.RegistrationIndex);
            });

            for (int index = 0; index < _producers.Count; index++) {
                _producers[index].Producer.OffersChanged
                    .Subscribe(_ => Refresh())
                    .AddTo(_subscriptions);
            }

            Refresh();
        }

        public bool TryCreateContext(
            DispensedDocumentPresentation presentation,
            out IDocumentContext context) {
            context = null;
            if (presentation == null) return false;

            IDocumentProducer producer = null;
            for (int index = 0; index < _producers.Count; index++) {
                ProducerEntry entry = _producers[index];
                if (entry.RegistrationIndex != presentation.ProducerRegistrationIndex) continue;
                producer = entry.Producer;
                break;
            }

            if (producer == null || !producer.TryProduce(presentation.Key, out IDocumentSession session)) {
                return false;
            }

            if (session == null) {
                throw new InvalidOperationException("A document producer returned success without a session.");
            }

            var properties = new DocumentProperties(session);
            try {
                properties.AddBehavior(presentation.TextSeed);
                properties.AddBehavior(presentation.HeaderColor);
                context = properties;
                return true;
            }
            catch {
                properties.Dispose();
                throw;
            }
        }

        public void RefreshCurrent() {
            Refresh();
        }

        public void AdvanceAfterClaim(DocumentOfferKey claimedKey) {
            Refresh();
            if (claimedKey.Kind != DocumentKind.Normal || _current == null || !_current.IsAvailable ||
                _current.Key != claimedKey) {
                return;
            }

            Refresh(true);
        }

        public void Dispose() {
            _subscriptions.Dispose();
            _changed.Dispose();
            _current = null;
            _currentOffer = null;
            _producers.Clear();
        }

        private void Refresh(bool forceNewPresentation = false) {
            if (!TryResolveOffer(out ProducerEntry entry, out DocumentOffer offer)) {
                if (_current == null) return;
                _current = null;
                _currentOffer = null;
                _changed.OnNext(null);
                return;
            }

            if (!forceNewPresentation && _currentOffer != null && _currentOffer.Equals(offer)) return;

            _currentOffer = offer;
            _current = CreatePresentation(entry, offer);
            _changed.OnNext(_current);
        }

        private bool TryResolveOffer(out ProducerEntry selected, out DocumentOffer offer) {
            for (int index = 0; index < _producers.Count; index++) {
                ProducerEntry entry = _producers[index];
                if (!entry.Producer.TryPeekOffer(out offer)) continue;
                selected = entry;
                return true;
            }

            selected = default;
            offer = null;
            return false;
        }

        private DispensedDocumentPresentation CreatePresentation(ProducerEntry entry, DocumentOffer offer) {
            float initialHue = 1f - 0.225f;
            float hue = initialHue - _documents.Value.SelectedDocumentQualityLevel * 0.075f;
            return new DispensedDocumentPresentation(
                offer,
                entry.RegistrationIndex,
                ++_nextRevision,
                _random.NextUInt64(),
                Color.HSVToRGB(hue, 0.8f, 0.8f));
        }

        private readonly struct ProducerEntry {
            public IDocumentProducer Producer { get; }
            public int RegistrationIndex { get; }

            public ProducerEntry(IDocumentProducer producer, int registrationIndex) {
                Producer = producer;
                RegistrationIndex = registrationIndex;
            }
        }
    }
}
