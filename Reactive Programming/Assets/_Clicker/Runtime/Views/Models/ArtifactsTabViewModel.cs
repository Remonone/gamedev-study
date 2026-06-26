using System;
using System.Collections.Generic;
using System.Linq;
using R3;
using Services;
using Types;
using UnityEngine;

namespace Views.Models {
    [Serializable]
    public struct ArtifactViewData {
        public string Id;
        public string Name;
        public string Description;
        public Sprite Icon;
        public bool IsUnlocked;
        public PracticeRarity Rarity;
    }

    public class ArtifactsTabViewModel : IDisposable {
        private readonly PracticeService _practiceService;
        private readonly CompositeDisposable _disposable = new();

        public ReactiveProperty<IReadOnlyList<ArtifactViewData>> Artifacts = new(Array.Empty<ArtifactViewData>());
        public ReactiveProperty<ArtifactViewData> SelectedArtifact = new(new ArtifactViewData());

        public ArtifactsTabViewModel(PracticeService practiceService) {
            _practiceService = practiceService;
            if (_practiceService == null) {
                return;
            }

            _practiceService.OwnedPractices
                .Subscribe(UpdateArtifacts)
                .AddTo(_disposable);
        }

        public void RequestInitialState() {
            UpdateArtifacts(_practiceService?.OwnedPracticeDefinitions ?? Array.Empty<Practice>());
        }

        public void SelectArtifact(string artifactId) {
            var artifact = Artifacts.Value.FirstOrDefault(item => item.Id == artifactId);
            if (!string.IsNullOrWhiteSpace(artifact.Id)) {
                SelectedArtifact.Value = artifact;
            }
        }

        public void Dispose() {
            _disposable.Dispose();
            Artifacts.Dispose();
            SelectedArtifact.Dispose();
        }

        private void UpdateArtifacts(IReadOnlyList<Practice> practices) {
            var artifacts = practices?
                .Where(practice => practice != null)
                .Select(ToViewData)
                .ToArray() ?? Array.Empty<ArtifactViewData>();

            Artifacts.Value = artifacts;
            if (artifacts.Length == 0) {
                SelectedArtifact.Value = new ArtifactViewData();
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedArtifact.Value.Id) || artifacts.All(item => item.Id != SelectedArtifact.Value.Id)) {
                SelectedArtifact.Value = artifacts[0];
            }
        }

        private ArtifactViewData ToViewData(Practice practice) {
            return new ArtifactViewData {
                Id = practice.Id,
                Name = practice.DisplayName,
                Description = BuildDescription(practice),
                Icon = practice.Icon,
                IsUnlocked = true,
                Rarity = practice.Rarity
            };
        }

        private string BuildDescription(Practice practice) {
            var description = string.IsNullOrWhiteSpace(practice.Description) ? "No description." : practice.Description;
            var effectsCount = practice.StatModifiers.Count + practice.InfluenceEffects.Count + practice.ResearchEffects.Count;
            return $"{description}\n\nRarity: {practice.Rarity}\nEffects: {effectsCount}";
        }
    }
}
