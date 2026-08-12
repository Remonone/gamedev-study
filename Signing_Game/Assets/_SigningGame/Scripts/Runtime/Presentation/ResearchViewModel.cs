using System;
using System.Collections.Generic;
using System.Globalization;
using Data.Research;
using R3;
using Services;
using UnityEngine;
using Utils;

namespace Presentation {
    public sealed class ResearchViewModel : IDisposable {
        private readonly ResearchService _research;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly ReactiveProperty<bool> _availability = new(false);
        private readonly Subject<Unit> _changed = new();
        private readonly List<ResearchOfferPresentationModel> _offers = new();
        private readonly List<ActivePracticePresentationModel> _active = new();

        public Observable<bool> Availability => _availability;
        public Observable<Unit> Changed => _changed;
        public IReadOnlyList<ResearchOfferPresentationModel> Offers => _offers;
        public IReadOnlyList<ActivePracticePresentationModel> Active => _active;
        public double Progress => _research.Progress;
        public double RequiredPoints => _research.RequiredPoints;
        public float NormalizedProgress => RequiredPoints <= 0d ? 0f : (float)Math.Clamp(Progress / RequiredPoints, 0d, 1d);
        public bool HasPendingSignature => _research.Pending.HasValue;
        public bool CanSell => _research.IsUnlocked && _research.CurrentOffers.Count > 0 && !_research.Pending.HasValue;
        public Value SalePayout => _research.SalePayout;
        public string ProgressText => $"{Progress.ToString("0.##", CultureInfo.InvariantCulture)} / {RequiredPoints.ToString("0.##", CultureInfo.InvariantCulture)}";
        public string StatusText => HasPendingSignature
            ? "The selected practice is awaiting signature."
            : _offers.Count > 0
                ? "Choose one practice or sell the whole offer."
                : !_research.HasConfiguredPractices
                    ? "No practices are configured."
                    : Progress >= RequiredPoints
                        ? "No practice is currently eligible."
                        : "Archive analysis is in progress.";

        public ResearchViewModel(ResearchService research) {
            _research = research ?? throw new ArgumentNullException(nameof(research));
            _research.Changed.Subscribe(_ => Rebuild()).AddTo(_subscriptions);
            Rebuild();
        }

        public bool SelectPractice(string practiceId) => _research.TrySelectPractice(practiceId);
        public bool SellOffer() => _research.TrySellOffer();

        public void Dispose() {
            _subscriptions.Dispose();
            _availability.Dispose();
            _changed.Dispose();
            _offers.Clear();
            _active.Clear();
        }

        private void Rebuild() {
            _availability.Value = _research.IsUnlocked;
            _offers.Clear();
            IReadOnlyList<PracticeDefinition> offers = _research.CurrentOffers;
            for (int index = 0; index < offers.Count; index++) {
                PracticeDefinition practice = offers[index];
                _research.TryGetRarityPresentation(practice.RarityId, out string rarityName, out Color rarityColor);
                _offers.Add(new ResearchOfferPresentationModel(
                    practice.Id,
                    practice.DisplayName,
                    practice.Description,
                    rarityName,
                    rarityColor,
                    practice.Icon));
            }
            _active.Clear();
            IReadOnlyList<ActivePracticeState> active = _research.ActivePractices;
            for (int index = 0; index < active.Count; index++) {
                ActivePracticeState state = active[index];
                string duration = state.IsPermanent
                    ? "Permanent"
                    : $"{Math.Max(0, (int)Math.Ceiling(state.RemainingSeconds)).ToString(CultureInfo.InvariantCulture)}s";
                _active.Add(new ActivePracticePresentationModel(
                    state.Definition.DisplayName,
                    state.Definition.Description,
                    duration,
                    state.Definition.Icon));
            }
            _changed.OnNext(Unit.Default);
        }
    }
}
