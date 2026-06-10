using System;
using R3;
using UnityEngine.UIElements;
using Views.Models;

namespace Views {
    public class ArtifactsTabView : IDisposable {
        private readonly VisualElement _root;
        private readonly VisualElement _selectedIcon;
        private readonly Label _selectedName;
        private readonly Label _selectedDescription;

        private CompositeDisposable _disposable = new();

        public ArtifactsTabView(VisualElement root) {
            _root = root;
            _selectedIcon = _root.Q<VisualElement>("SelectedArtifactIcon");
            _selectedName = _root.Q<Label>("SelectedArtifactName");
            _selectedDescription = _root.Q<Label>("SelectedArtifactDescription");
        }

        public void Bind(ArtifactsTabViewModel viewModel) {
            _disposable.Dispose();
            _disposable = new CompositeDisposable();

            if (viewModel == null) {
                return;
            }

            viewModel.SelectedArtifact.Subscribe(UpdateSelectedArtifact).AddTo(_disposable);
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
                return;
            }

            _selectedIcon.style.backgroundImage = new StyleBackground(artifact.Icon);
        }
    }
}
