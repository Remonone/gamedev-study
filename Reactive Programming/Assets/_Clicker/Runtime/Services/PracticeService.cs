using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using R3;
using Save;
using Types;
using Types.Enums;
using Types.Modifiers;
using Types.Practices;
using UnityEngine;

namespace Services {
    public sealed class PracticeService : IService, ISaveable, IDisposable {

        private readonly Dictionary<string, Practice> _practicesById = new();
        private readonly List<Practice> _practiceDefinitions = new();
        private readonly List<string> _ownedPracticeIds = new();
        private readonly PracticeRewardConfig _rewardConfig;
        private readonly InvalidationService _invalidationService;
        private readonly System.Random _random;

        private readonly ReactiveProperty<IReadOnlyList<Practice>> _ownedPractices = new(Array.Empty<Practice>());
        private readonly ReactiveProperty<PracticeOfferState> _currentOffer = new(PracticeOfferState.Empty);
        private readonly Subject<PracticeRarity> _onRecycleRarity = new();
        private readonly Subject<Unit> _changed = new();

        private PracticeOfferData _offer = PracticeOfferData.Inactive;

        public PracticeService(
            IEnumerable<Practice> practiceDefinitions,
            PracticeRewardConfig rewardConfig,
            InvalidationService invalidationService,
            SessionContext sessionContext) {
            _rewardConfig = rewardConfig;
            _invalidationService = invalidationService;
            _random = new System.Random(sessionContext?.Seed ?? Environment.TickCount);
            RegisterDefinitions(practiceDefinitions);
            PublishOwnedPractices();
        }

        public Observable<IReadOnlyList<Practice>> OwnedPractices => _ownedPractices;
        public Observable<PracticeOfferState> CurrentOffer => _currentOffer;
        public Observable<Unit> Changed => _changed;
        public IReadOnlyList<Practice> OwnedPracticeDefinitions => _ownedPractices.Value;
        public Observable<PracticeRarity> OnRecycleRarity => _onRecycleRarity;

        public string SaveKey => "Practices";
        public int Priority => 71;

        public bool BeginResearchOffer(PracticeRarity rarity) {
            if (_offer.IsActive) {
                _offer.IsVisible = true;
                PublishOffer();
                return _offer.OfferedPracticeIds.Count > 0;
            }

            var offered = SelectPracticeOffer(rarity);
            if (offered.Count == 0) {
                return false;
            }

            _offer = new PracticeOfferData {
                IsActive = true,
                IsVisible = true,
                Rarity = rarity,
                OfferedPracticeIds = offered,
                SelectedPracticeId = offered[0]
            };
            PublishOffer();
            return true;
        }

        public void CancelOffer() {
            if (_offer.IsActive) {
                _offer.IsVisible = false;
            }
            PublishOffer();
        }

        public void SelectOfferedPractice(string practiceId) {
            if (!_offer.IsActive || string.IsNullOrWhiteSpace(practiceId)) {
                return;
            }

            if (!_offer.OfferedPracticeIds.Contains(practiceId)) {
                return;
            }

            _offer.SelectedPracticeId = practiceId;
            PublishOffer();
        }

        public bool ConfirmSelectedOffer() {
            if (!_offer.IsActive || string.IsNullOrWhiteSpace(_offer.SelectedPracticeId)) {
                return false;
            }

            if (!_practicesById.ContainsKey(_offer.SelectedPracticeId)) {
                return false;
            }

            AddPractice(_offer.SelectedPracticeId);
            _offer = PracticeOfferData.Inactive;
            PublishOffer();
            return true;
        }

        public bool RecycleSelectedOffer() {
            if (!_offer.IsActive || string.IsNullOrWhiteSpace(_offer.SelectedPracticeId)) {
                return false;
            }

            _onRecycleRarity.OnNext(_offer.Rarity);
            _offer = PracticeOfferData.Inactive;
            PublishOffer();
            NotifyChanged();
            return true;
        }

        public ResearchPracticeModifiers GetResearchModifiers() {
            var result = ResearchPracticeModifiers.Default;
            foreach (var practice in _ownedPracticeIds.Select(GetPractice).Where(practice => practice != null)) {
                foreach (var effect in practice.ResearchEffects) {
                    ApplyResearchEffect(ref result, effect);
                }
            }

            return result;
        }

        public JToken Save() {
            return new JObject(
                new JProperty("OwnedPracticeIds", new JArray(_ownedPracticeIds)),
                new JProperty("Offer", SaveOffer()));
        }

        public void Load(JToken data) {
            if (data == null) {
                return;
            }

            _ownedPracticeIds.Clear();
            foreach (var id in data["OwnedPracticeIds"]?.Values<string>() ?? Enumerable.Empty<string>()) {
                if (_practicesById.ContainsKey(id) && !_ownedPracticeIds.Contains(id)) {
                    _ownedPracticeIds.Add(id);
                }
            }

            _offer = LoadOffer(data["Offer"]);
            PublishOwnedPractices();
            PublishOffer();
            NotifyChanged();
        }

        public void Dispose() {
            _ownedPractices.Dispose();
            _currentOffer.Dispose();
            _changed.Dispose();
        }

        private void RegisterDefinitions(IEnumerable<Practice> practiceDefinitions) {
            if (practiceDefinitions == null) {
                return;
            }

            foreach (var practice in practiceDefinitions) {
                if (practice == null || string.IsNullOrWhiteSpace(practice.Id)) {
                    continue;
                }

                if (_practicesById.ContainsKey(practice.Id)) {
                    Debug.LogWarning($"Duplicate practice id skipped: {practice.Id}");
                    continue;
                }

                _practicesById.Add(practice.Id, practice);
                _practiceDefinitions.Add(practice);
            }
        }

        private List<string> SelectPracticeOffer(PracticeRarity rarity) {
            var choicesCount = _rewardConfig != null ? _rewardConfig.ChoicesCount : 3;
            var candidates = _practiceDefinitions
                .Where(practice => practice.Rarity == rarity && practice.Weight > 0f && !_ownedPracticeIds.Contains(practice.Id))
                .ToList();

            if (candidates.Count < choicesCount) {
                var ownedCandidates = _practiceDefinitions
                    .Where(practice => practice.Rarity == rarity && practice.Weight > 0f && !candidates.Contains(practice))
                    .ToList();
                candidates.AddRange(ownedCandidates);
            }

            var result = new List<string>(choicesCount);
            while (result.Count < choicesCount && candidates.Count > 0) {
                var selected = PickWeighted(candidates);
                if (selected == null) {
                    break;
                }

                result.Add(selected.Id);
                candidates.Remove(selected);
            }

            return result;
        }

        private Practice PickWeighted(List<Practice> candidates) {
            var totalWeight = 0f;
            foreach (var candidate in candidates) {
                totalWeight += candidate.Weight;
            }

            if (totalWeight <= 0f) {
                return null;
            }

            var roll = _random.NextDouble() * totalWeight;
            var cumulative = 0d;
            foreach (var candidate in candidates) {
                cumulative += candidate.Weight;
                if (roll <= cumulative) {
                    return candidate;
                }
            }

            return candidates[candidates.Count - 1];
        }

        private void AddPractice(string practiceId) {
            if (_ownedPracticeIds.Contains(practiceId)) {
                return;
            }

            _ownedPracticeIds.Add(practiceId);
            PublishOwnedPractices();
            NotifyChanged();
        }

        private void ApplyResearchEffect(ref ResearchPracticeModifiers modifiers, ResearchPracticeEffect effect) {
            switch (effect.Type) {
                case PracticeResearchModifierType.PointsPerSecondMultiplier:
                    modifiers.PointsPerSecondMultiplier = ApplyFloatModifier(modifiers.PointsPerSecondMultiplier, effect.Operation, effect.Value);
                    break;
                case PracticeResearchModifierType.RequiredPointsMultiplier:
                    modifiers.RequiredPointsMultiplier = Mathf.Max(0.0001f, ApplyFloatModifier(modifiers.RequiredPointsMultiplier, effect.Operation, effect.Value));
                    break;
                case PracticeResearchModifierType.FlatPointsPerSecond:
                    modifiers.FlatPointsPerSecond = ApplyFloatModifier(modifiers.FlatPointsPerSecond, effect.Operation, effect.Value);
                    break;
            }
        }

        private float ApplyFloatModifier(float current, ModifierOp operation, float value) {
            return operation switch {
                ModifierOp.AddFlat => current + value,
                ModifierOp.AddPercent => current * (1f + value),
                ModifierOp.Multiply => current * value,
                ModifierOp.Override => value,
                _ => current
            };
        }

        private Practice GetPractice(string id) {
            return !string.IsNullOrWhiteSpace(id) && _practicesById.TryGetValue(id, out var practice) ? practice : null;
        }

        private void PublishOwnedPractices() {
            _ownedPractices.Value = _ownedPracticeIds.Select(GetPractice).Where(practice => practice != null).ToArray();
        }

        private void PublishOffer() {
            if (!_offer.IsActive || !_offer.IsVisible) {
                _currentOffer.Value = PracticeOfferState.Empty;
                return;
            }

            var offered = _offer.OfferedPracticeIds.Select(GetPractice).Where(practice => practice != null).ToArray();
            var selected = GetPractice(_offer.SelectedPracticeId) ?? offered.FirstOrDefault();
            _currentOffer.Value = new PracticeOfferState(true, _offer.Rarity, offered, selected?.Id, selected);
        }

        private void NotifyChanged() {
            _invalidationService.InvalidateAll();
            _changed.OnNext(Unit.Default);
        }

        private JObject SaveOffer() {
            return new JObject(
                new JProperty("IsActive", _offer.IsActive),
                new JProperty("IsVisible", _offer.IsVisible),
                new JProperty("Rarity", _offer.Rarity.ToString()),
                new JProperty("OfferedPracticeIds", new JArray(_offer.OfferedPracticeIds)),
                new JProperty("SelectedPracticeId", _offer.SelectedPracticeId));
        }

        private PracticeOfferData LoadOffer(JToken token) {
            if (token is not JObject obj || !(obj.Value<bool?>("IsActive") ?? false)) {
                return PracticeOfferData.Inactive;
            }

            if (!Enum.TryParse(obj.Value<string>("Rarity"), out PracticeRarity rarity)) {
                rarity = PracticeRarity.Common;
            }

            var offeredIds = obj["OfferedPracticeIds"]?.Values<string>()
                .Where(id => _practicesById.ContainsKey(id))
                .Distinct()
                .ToList() ?? new List<string>();
            if (offeredIds.Count == 0) {
                return PracticeOfferData.Inactive;
            }

            var selectedId = obj.Value<string>("SelectedPracticeId");
            if (!offeredIds.Contains(selectedId)) {
                selectedId = offeredIds[0];
            }

            return new PracticeOfferData {
                IsActive = true,
                IsVisible = obj.Value<bool?>("IsVisible") ?? false,
                Rarity = rarity,
                OfferedPracticeIds = offeredIds,
                SelectedPracticeId = selectedId
            };
        }
    }

    public readonly struct PracticeOfferState {
        public readonly bool IsActive;
        public readonly PracticeRarity Rarity;
        public readonly IReadOnlyList<Practice> OfferedPractices;
        public readonly string SelectedPracticeId;
        public readonly Practice SelectedPractice;

        public PracticeOfferState(bool isActive, PracticeRarity rarity, IReadOnlyList<Practice> offeredPractices, string selectedPracticeId, Practice selectedPractice) {
            IsActive = isActive;
            Rarity = rarity;
            OfferedPractices = offeredPractices ?? Array.Empty<Practice>();
            SelectedPracticeId = selectedPracticeId;
            SelectedPractice = selectedPractice;
        }

        public static PracticeOfferState Empty => new PracticeOfferState(false, PracticeRarity.Common, Array.Empty<Practice>(), null, null);
    }

    [Serializable]
    public class PracticeRecycleModifier {
        public string TargetBuildingName;
        public bool AppliesToAllClicks;
        public StatType Stat;
        public ModifierOp Operation;
        public float Value;
        public int Priority;
        public string ModifierId;
    }

    internal class PracticeOfferData {
        public bool IsActive;
        public bool IsVisible;
        public PracticeRarity Rarity;
        public List<string> OfferedPracticeIds = new();
        public string SelectedPracticeId;

        public static PracticeOfferData Inactive => new PracticeOfferData { IsActive = false };
    }
}
