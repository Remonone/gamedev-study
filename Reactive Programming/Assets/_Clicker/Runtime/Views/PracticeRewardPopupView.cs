using System;
using System.Collections.Generic;
using R3;
using UnityEngine.UIElements;
using Views.Models;

namespace Views {
    public sealed class PracticeRewardPopupView : IDisposable {
        private readonly VisualElement _root;
        private readonly Label _title;
        private readonly VisualElement _optionsContainer;
        private readonly VisualElement _selectedIcon;
        private readonly Label _selectedName;
        private readonly Label _selectedDescription;
        private readonly Button _confirmButton;
        private readonly Button _recycleButton;
        private readonly Button _cancelButton;
        private readonly Dictionary<string, Button> _optionButtons = new();
        private readonly CompositeDisposable _disposable = new();

        private PracticeRewardPopupViewModel _viewModel;

        public PracticeRewardPopupView(VisualElement root) {
            _root = root;
            _title = _root?.Q<Label>("PracticeRewardTitle");
            _optionsContainer = _root?.Q<VisualElement>("PracticeRewardOptions");
            _selectedIcon = _root?.Q<VisualElement>("PracticeRewardSelectedIcon");
            _selectedName = _root?.Q<Label>("PracticeRewardSelectedName");
            _selectedDescription = _root?.Q<Label>("PracticeRewardSelectedDescription");
            _confirmButton = _root?.Q<Button>("PracticeRewardConfirmButton");
            _recycleButton = _root?.Q<Button>("PracticeRewardRecycleButton");
            _cancelButton = _root?.Q<Button>("PracticeRewardCancelButton");
        }

        public void Bind(PracticeRewardPopupViewModel viewModel) {
            _viewModel = viewModel;
            if (_root == null || viewModel == null) {
                return;
            }

            _confirmButton.clicked += OnConfirmClicked;
            _recycleButton.clicked += OnRecycleClicked;
            _cancelButton.clicked += OnCancelClicked;

            viewModel.IsVisible.Subscribe(SetVisible).AddTo(_disposable);
            viewModel.TitleText.Subscribe(value => _title.text = value).AddTo(_disposable);
            viewModel.Options.Subscribe(UpdateOptions).AddTo(_disposable);
            viewModel.SelectedOption.Subscribe(UpdateSelected).AddTo(_disposable);
            viewModel.HasSelection.Subscribe(SetHasSelection).AddTo(_disposable);
        }

        public void Dispose() {
            if (_confirmButton != null) {
                _confirmButton.clicked -= OnConfirmClicked;
            }

            if (_recycleButton != null) {
                _recycleButton.clicked -= OnRecycleClicked;
            }

            if (_cancelButton != null) {
                _cancelButton.clicked -= OnCancelClicked;
            }

            _disposable.Dispose();
        }

        private void SetVisible(bool visible) {
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdateOptions(IReadOnlyList<PracticeRewardOptionViewData> options) {
            _optionsContainer.Clear();
            _optionButtons.Clear();

            if (options == null || options.Count == 0) {
                _optionsContainer.Add(new Label("No practices available."));
                return;
            }

            foreach (var option in options) {
                var button = new Button(() => _viewModel?.SelectPractice(option.Id));
                button.AddToClassList("practice-reward-option");
                button.AddToClassList($"practice-reward-option--{option.Rarity.ToString().ToLowerInvariant()}");

                var icon = new VisualElement();
                icon.AddToClassList("practice-reward-option__icon");
                if (option.Icon != null) {
                    icon.style.backgroundImage = new StyleBackground(option.Icon);
                }

                var label = new Label(option.Name);
                label.AddToClassList("practice-reward-option__name");
                button.Add(icon);
                button.Add(label);
                _optionButtons[option.Id] = button;
                _optionsContainer.Add(button);
            }

            UpdateSelectedHighlight();
        }

        private void UpdateSelected(PracticeRewardOptionViewData selected) {
            _selectedName.text = string.IsNullOrWhiteSpace(selected.Name) ? "No practice selected" : selected.Name;
            _selectedDescription.text = string.IsNullOrWhiteSpace(selected.Description) ? "Select a practice to inspect it." : selected.Description;
            _selectedIcon.style.backgroundImage = selected.Icon != null ? new StyleBackground(selected.Icon) : new StyleBackground();
            UpdateSelectedHighlight();
        }

        private void UpdateSelectedHighlight() {
            var selectedId = _viewModel?.SelectedOption.Value.Id;
            foreach (var pair in _optionButtons) {
                pair.Value.EnableInClassList("practice-reward-option--selected", pair.Key == selectedId);
            }
        }

        private void SetHasSelection(bool hasSelection) {
            _confirmButton.SetEnabled(hasSelection);
            _recycleButton.SetEnabled(hasSelection);
        }

        private void OnConfirmClicked() {
            _viewModel?.ConfirmSelectedPractice();
        }

        private void OnRecycleClicked() {
            _viewModel?.RecycleSelectedPractice();
        }

        private void OnCancelClicked() {
            _viewModel?.Cancel();
        }
    }
}
