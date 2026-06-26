using System;
using R3;
using UnityEngine.UIElements;
using Views.Models;

namespace Views {
    public class ResearchTabView : IDisposable {
        private readonly VisualElement _tab;
        private readonly VisualElement _root;
        private readonly CompositeDisposable _disposable = new();

        private Label _completedLabel;
        private Label _nextCostLabel;
        private Label _investedLabel;
        private Label _pointsPerSecondLabel;
        private Label _scaleModifierLabel;
        private ProgressBar _progressBar;
        private Button _completeButton;
        private ResearchTabViewModel _viewModel;

        public ResearchTabView(VisualElement tab, VisualElement root) {
            _tab = tab;
            _root = root;
        }

        public void Bind(ResearchTabViewModel viewModel) {
            if (_root == null || _tab == null || viewModel == null) {
                return;
            }

            _viewModel = viewModel;
            _completedLabel = _root.Q<Label>("ResearchCompletedCount");
            _nextCostLabel = _root.Q<Label>("ResearchNextCost");
            _investedLabel = _root.Q<Label>("ResearchInvestedPoints");
            _pointsPerSecondLabel = _root.Q<Label>("ResearchPointsPerSecond");
            _scaleModifierLabel = _root.Q<Label>("ResearchScaleModifier");
            _progressBar = _root.Q<ProgressBar>("ResearchProgress");
            _completeButton = _root.Q<Button>("CompleteResearchButton");

            if (_progressBar != null) {
                _progressBar.lowValue = 0f;
                _progressBar.highValue = 1f;
            }

            _completeButton.clicked += OnCompleteClicked;

            viewModel.IsUnlocked.Subscribe(SetUnlocked).AddTo(_disposable);
            viewModel.CompletedText.Subscribe(value => _completedLabel.text = value).AddTo(_disposable);
            viewModel.NextCostText.Subscribe(value => _nextCostLabel.text = value).AddTo(_disposable);
            viewModel.InvestedText.Subscribe(value => _investedLabel.text = value).AddTo(_disposable);
            viewModel.PointsPerSecondText.Subscribe(value => _pointsPerSecondLabel.text = value).AddTo(_disposable);
            viewModel.ScaleModifierText.Subscribe(value => _scaleModifierLabel.text = value).AddTo(_disposable);
            viewModel.Progress.Subscribe(SetProgress).AddTo(_disposable);
            viewModel.CanComplete.Subscribe(canComplete => _completeButton.SetEnabled(canComplete)).AddTo(_disposable);
        }

        private void SetUnlocked(bool isUnlocked) {
            _tab.EnableInClassList("locked", !isUnlocked);
        }

        private void SetProgress(float progress) {
            _progressBar.value = progress;
            _progressBar.title = $"{progress * 100f:0}%";
        }

        private void OnCompleteClicked() {
            _viewModel?.CompleteResearch();
        }

        public void Dispose() {
            if (_completeButton != null) {
                _completeButton.clicked -= OnCompleteClicked;
            }

            _disposable.Dispose();
        }
    }
}
