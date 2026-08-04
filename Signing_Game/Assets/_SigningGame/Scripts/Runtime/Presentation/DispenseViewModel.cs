using System;
using System.Collections.Generic;
using Contracts;
using Data.Cache;
using Data.Documents;
using UnityEngine;
using Utils.Text.Generator;

namespace Presentation {
    public sealed class DispenseViewModel {
        private readonly List<ProducerEntry> _producers = new();
        private readonly IReadOnlyCacheData<DocumentEntries> _documents;
        private StableRandom _random;

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
        }

        public bool TryCreateContext(out IDocumentContext context) {
            context = null;
            for (int index = 0; index < _producers.Count; index++) {
                if (!_producers[index].Producer.TryProduce(out IDocumentSession session)) continue;
                if (session == null) {
                    throw new InvalidOperationException("A document producer returned success without a session.");
                }

                var properties = new DocumentProperties(session);
                try {
                    properties.AddBehavior(_random.NextUInt64());
                    float initialHue = 1f - 0.225f;
                    float hue = initialHue - _documents.Value.SelectedDocumentQualityLevel * 0.075f;
                    properties.AddBehavior(Color.HSVToRGB(hue, 0.8f, 0.8f));
                    context = properties;
                    return true;
                }
                catch {
                    properties.Dispose();
                    throw;
                }
            }

            return false;
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
