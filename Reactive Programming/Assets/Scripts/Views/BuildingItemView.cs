using R3;
using Types.Enums;
using UnityEngine;
using UnityEngine.UIElements;
using Views.Models;

namespace Views {
    public class BuildingItemView : MonoBehaviour {
        private const string CardStylePath = "UI/Styles/card";

        [SerializeField, Tooltip("UIDocument instance used as the building card template root.")]
        private UIDocument _item;
        private static StyleSheet _cardStyleSheet;
        
        private VisualElement _root;
        private VisualElement _container;
        private BuildingItemViewModel _viewModel;

        private VisualElement _cardRoot;
        private VisualElement _icon;
        private Label _name;
        private Label _description;
        private Label _incomeLabel;
        private Label _frequencyLabel;
        private Button _upgradeButton;
        private Button _expandButton;
        private Label _stabilityLabel;
        private Label _stabilityMultiplierLabel;
        private Label _multiplierLabel;
        private Label _criticalChanceLabel;
        private Label _criticalMultiplierLabel;
        private bool _isExpanded;

        public void Bind(BuildingItemViewModel viewModel, VisualElement container) {
            _root = _item.rootVisualElement;
            _container = container;
            _viewModel = viewModel;

            EnsureStyleSheet();
            SetRootLayout();
            
            var element = GetCategoryByType(_container, viewModel.Type);
            
            element.Add(_root);
            
            SetProps();
            
            viewModel.Income.Subscribe(income => _incomeLabel.text = $"{income}").AddTo(this);
            viewModel.Frequency.Subscribe(frequency => _frequencyLabel.text = $"{frequency}").AddTo(this);
            viewModel.Cost.Subscribe(cost => _upgradeButton.text = $"${cost}").AddTo(this);
            viewModel.Stability.Subscribe(stability => _stabilityLabel.text = $"{stability}").AddTo(this);
            viewModel.StabilityMultiplier.Subscribe(stabilityMultiplier => _stabilityMultiplierLabel.text = $"{stabilityMultiplier}").AddTo(this);
            viewModel.Multiplier.Subscribe(multiplier => _multiplierLabel.text = $"{multiplier}").AddTo(this);
            viewModel.CriticalChance.Subscribe(criticalChance => _criticalChanceLabel.text = $"{criticalChance}").AddTo(this);
            viewModel.CriticalMultiplier.Subscribe(criticalMultiplier => _criticalMultiplierLabel.text = $"{criticalMultiplier}").AddTo(this);
            viewModel.CanPurchase.Subscribe(canPurchase => _upgradeButton.SetEnabled(canPurchase)).AddTo(this);
            viewModel.Description.Subscribe(description => _description.text = description).AddTo(this);
            viewModel.Icon.Subscribe(SetIcon).AddTo(this);
        }

        private void SetRootLayout() {
            _root.style.flexGrow = 0;
            _root.style.flexShrink = 0;
            _root.style.alignSelf = Align.Stretch;
            _root.style.width = Length.Percent(100);
        }

        private VisualElement GetCategoryByType(VisualElement container, GovernmentInteractionType type) {
            var typeName = type.ToString();
            
            return container.Q<VisualElement>(typeName);
        }

        private void EnsureStyleSheet() {
            if (_cardStyleSheet == null) {
                _cardStyleSheet = Resources.Load<StyleSheet>(CardStylePath);
            }

            if (_cardStyleSheet != null) {
                _root.styleSheets.Add(_cardStyleSheet);
            }
        }

        private void SetProps() {
            _cardRoot = _root.Q<VisualElement>("CardRoot");
            _icon = _root.Q<VisualElement>("Icon");
            _name = _root.Q<Label>("Name");
            _description = _root.Q<Label>("Description");
            _incomeLabel = _root.Q<Label>("Income");
            _frequencyLabel = _root.Q<Label>("Frequency");
            _upgradeButton = _root.Q<Button>("UpgradeButton");
            _expandButton = _root.Q<Button>("ExpandButton");
            _stabilityLabel = _root.Q<Label>("Stability");
            _stabilityMultiplierLabel = _root.Q<Label>("StabilityMultiplier");
            _multiplierLabel = _root.Q<Label>("Multiplier");
            _criticalChanceLabel = _root.Q<Label>("CriticalChance");
            _criticalMultiplierLabel = _root.Q<Label>("CriticalMultiplier");
            
            _upgradeButton.clicked += () => _viewModel.Upgrade();
            _expandButton.clicked += ToggleDetails;
            
            _name.text = _viewModel.Name;
            _description.text = _viewModel.Description.Value;
            SetIcon(_viewModel.Icon.Value);
            SetExpanded(false);
        }

        private void ToggleDetails() {
            SetExpanded(!_isExpanded);
        }

        private void SetExpanded(bool expanded) {
            _isExpanded = expanded;

            if (_isExpanded) {
                _cardRoot.AddToClassList("building-card--expanded");
                _expandButton.text = "Hide";
            } else {
                _cardRoot.RemoveFromClassList("building-card--expanded");
                _expandButton.text = "Details";
            }
        }

        private void SetIcon(Sprite icon) {
            if (_icon == null) {
                return;
            }

            if (icon == null) {
                _icon.style.backgroundImage = new StyleBackground();
                _icon.AddToClassList("building-card__icon--empty");
                return;
            }

            _icon.style.backgroundImage = new StyleBackground(icon);
            _icon.RemoveFromClassList("building-card__icon--empty");
        }
    }
}
