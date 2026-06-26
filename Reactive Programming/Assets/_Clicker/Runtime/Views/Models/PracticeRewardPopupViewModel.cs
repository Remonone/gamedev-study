using System;
using System.Collections.Generic;
using System.Linq;
using R3;
using Services;
using Types;
using UnityEngine;

namespace Views.Models {
    public readonly struct PracticeRewardOptionViewData {
        public readonly string Id;
        public readonly string Name;
        public readonly string Description;
        public readonly Sprite Icon;
        public readonly PracticeRarity Rarity;
        public readonly bool IsSelected;

        public PracticeRewardOptionViewData(string id, string name, string description, Sprite icon, PracticeRarity rarity, bool isSelected) {
            Id = id;
            Name = name;
            Description = description;
            Icon = icon;
            Rarity = rarity;
            IsSelected = isSelected;
        }
    }

    public sealed class PracticeRewardPopupViewModel : IDisposable {
        private readonly PracticeService _practiceService;
        private readonly ResearchService _researchService;
        private readonly CompositeDisposable _disposable = new();

        public ReactiveProperty<bool> IsVisible = new(false);
        public ReactiveProperty<string> TitleText = new("Choose practice");
        public ReactiveProperty<IReadOnlyList<PracticeRewardOptionViewData>> Options = new(Array.Empty<PracticeRewardOptionViewData>());
        public ReactiveProperty<PracticeRewardOptionViewData> SelectedOption = new(default);
        public ReactiveProperty<bool> HasSelection = new(false);

        public PracticeRewardPopupViewModel(PracticeService practiceService, ResearchService researchService) {
            _practiceService = practiceService;
            _researchService = researchService;

            if (_practiceService == null) {
                return;
            }

            _practiceService.CurrentOffer
                .Subscribe(UpdateOffer)
                .AddTo(_disposable);
        }

        public void SelectPractice(string practiceId) {
            _practiceService?.SelectOfferedPractice(practiceId);
        }

        public void ConfirmSelectedPractice() {
            if (_practiceService == null || _researchService == null) {
                return;
            }

            if (_practiceService.ConfirmSelectedOffer()) {
                _researchService.ClaimPendingResearch();
            }
        }

        public void RecycleSelectedPractice() {
            if (_practiceService == null || _researchService == null) {
                return;
            }

            if (_practiceService.RecycleSelectedOffer()) {
                _researchService.ClaimPendingResearch();
            }
        }

        public void Cancel() {
            _practiceService?.CancelOffer();
        }

        public void Dispose() {
            _disposable.Dispose();
            IsVisible.Dispose();
            TitleText.Dispose();
            Options.Dispose();
            SelectedOption.Dispose();
            HasSelection.Dispose();
        }

        private void UpdateOffer(PracticeOfferState offer) {
            IsVisible.Value = offer.IsActive;
            if (!offer.IsActive) {
                Options.Value = Array.Empty<PracticeRewardOptionViewData>();
                SelectedOption.Value = default;
                HasSelection.Value = false;
                return;
            }

            TitleText.Value = $"Choose {offer.Rarity} practice";
            var options = offer.OfferedPractices
                .Where(practice => practice != null)
                .Select(practice => ToOption(practice, practice.Id == offer.SelectedPracticeId))
                .ToArray();
            Options.Value = options;
            var selected = options.FirstOrDefault(option => option.IsSelected);
            SelectedOption.Value = selected;
            HasSelection.Value = !string.IsNullOrWhiteSpace(selected.Id);
        }

        private PracticeRewardOptionViewData ToOption(Practice practice, bool isSelected) {
            return new PracticeRewardOptionViewData(
                practice.Id,
                practice.DisplayName,
                string.IsNullOrWhiteSpace(practice.Description) ? "No description." : practice.Description,
                practice.Icon,
                practice.Rarity,
                isSelected);
        }
    }
}
