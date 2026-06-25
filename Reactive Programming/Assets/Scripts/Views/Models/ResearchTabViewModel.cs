using System;
using R3;
using Services;

namespace Views.Models {
    public class ResearchTabViewModel : IDisposable {
        private readonly ResearchService _researchService;
        private readonly CompositeDisposable _disposable = new();

        public ReactiveProperty<bool> IsUnlocked = new(false);
        public ReactiveProperty<string> CompletedText = new("0");
        public ReactiveProperty<string> NextCostText = new("0");
        public ReactiveProperty<string> InvestedText = new("0");
        public ReactiveProperty<string> PointsPerSecondText = new("0/s");
        public ReactiveProperty<string> ScaleModifierText = new("x1");
        public ReactiveProperty<float> Progress = new(0f);
        public ReactiveProperty<bool> CanComplete = new(false);

        public ResearchTabViewModel(ResearchService researchService) {
            _researchService = researchService;
            _researchService.State
                .Subscribe(state => {
                    IsUnlocked.Value = state.IsUnlocked;
                    CompletedText.Value = state.CompletedCount.ToString();
                    NextCostText.Value = state.NextCost.ToString();
                    InvestedText.Value = state.InvestedPoints.ToString();
                    PointsPerSecondText.Value = $"{state.PointsPerSecond}/s";
                    ScaleModifierText.Value = $"x{state.ScaleModifier:0.##}";
                    Progress.Value = state.Progress01;
                    CanComplete.Value = state.CanComplete;
                })
                .AddTo(_disposable);
        }

        public void CompleteResearch() {
            _researchService.CompleteCurrentResearch();
        }

        public void Dispose() {
            _disposable.Dispose();
            IsUnlocked.Dispose();
            CompletedText.Dispose();
            NextCostText.Dispose();
            InvestedText.Dispose();
            PointsPerSecondText.Dispose();
            ScaleModifierText.Dispose();
            Progress.Dispose();
            CanComplete.Dispose();
        }
    }
}
