using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using UnityEngine.UIElements;
using Views.Models;

namespace Views {
    public class ArtifactsTabView : IDisposable {
        private readonly VisualElement _root;
        private readonly VisualElement _selectedIcon;
        private readonly Label _selectedName;
        private readonly Label _selectedDescription;
        private readonly VisualElement _artifactsList;
        private readonly Dictionary<string, Button> _artifactButtons = new();

        private CompositeDisposable _disposable = new();
        private ArtifactsTabViewModel _viewModel;

        public ArtifactsTabView(VisualElement root) {
            _root = root;
            _selectedIcon = _root.Q<VisualElement>("SelectedArtifactIcon");
            _selectedName = _root.Q<Label>("SelectedArtifactName");
            _selectedDescription = _root.Q<Label>("SelectedArtifactDescription");
            var artifactsScroll = _root.Q<ScrollView>("ArtifactsList");
            _artifactsList = artifactsScroll?.contentContainer ?? _root.Q<VisualElement>("ArtifactsList");
        }

        public void Bind(ArtifactsTabViewModel viewModel) {
            _disposable.Dispose();
            _disposable = new CompositeDisposable();

            if (viewModel == null) {
                return;
            }

            _viewModel = viewModel;
            viewModel.SelectedArtifact.Subscribe(UpdateSelectedArtifact).AddTo(_disposable);
            viewModel.Artifacts.Subscribe(UpdateArtifacts).AddTo(_disposable);
            viewModel.RequestInitialState();
        }

        public void Dispose() {
            _disposable.Dispose();
        }

        private void UpdateSelectedArtifact(ArtifactViewData artifact) {
            _selectedName.text = string.IsNullOrWhiteSpace(artifact.Name) ? "No artifact selected" : artifact.Name;
            _selectedDescription.text = string.IsNullOrWhiteSpace(artifact.Description)
                ? "Select an artifact to see details."
                : artifact.Description;

            if (artifact.Icon == null) {
                _selectedIcon.style.backgroundImage = new StyleBackground();
                UpdateSelectedHighlight(artifact.Id);
                return;
            }

            _selectedIcon.style.backgroundImage = new StyleBackground(artifact.Icon);
            UpdateSelectedHighlight(artifact.Id);
        }

        private void UpdateArtifacts(IReadOnlyList<ArtifactViewData> artifacts) {
            if (_artifactsList == null) {
                return;
            }

            _artifactsList.Clear();
            _artifactButtons.Clear();

            if (artifacts == null || artifacts.Count == 0) {
                var emptyLabel = new Label("No practices acquired yet.");
                emptyLabel.AddToClassList("artifacts-placeholder-text");
                _artifactsList.Add(emptyLabel);
                return;
            }

            foreach (var artifact in artifacts) {
                var button = new Button(() => _viewModel?.SelectArtifact(artifact.Id));
                button.name = $"Artifact_{artifact.Id}";
                button.AddToClassList("artifact-tile");
                button.AddToClassList($"artifact-tile--{artifact.Rarity.ToString().ToLowerInvariant()}");

                var icon = new VisualElement();
                icon.AddToClassList("artifact-tile__icon");
                if (artifact.Icon != null) {
                    icon.style.backgroundImage = new StyleBackground(artifact.Icon);
                }

                button.tooltip = artifact.Name;
                button.Add(icon);
                _artifactButtons[artifact.Id] = button;
                _artifactsList.Add(button);
            }

            UpdateSelectedHighlight(_viewModel?.SelectedArtifact.Value.Id);
        }

        private void UpdateSelectedHighlight(string selectedId) {
            foreach (var pair in _artifactButtons) {
                pair.Value.EnableInClassList("artifact-tile--selected", pair.Key == selectedId);
            }
        }
    }
}
