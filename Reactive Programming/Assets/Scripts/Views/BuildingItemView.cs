using R3;
using Types;
using UnityEngine;
using UnityEngine.UIElements;
using Views.Models;

namespace Views {
    public class BuildingItemView : MonoBehaviour {
        [SerializeField] private UIDocument _item;
        
        private VisualElement _root;
        private VisualElement _container;
        private BuildingItemViewModel _viewModel;

        private Label _name;
        private Label _incomeLabel;
        private Label _frequencyLabel;
        private Button _upgradeButton;
        private Label _stabilityLabel;
        private Label _stabilityMultiplierLabel;
        private Label _multiplierLabel;
        private Label _criticalChanceLabel;
        private Label _criticalMultiplierLabel;

        public void Bind(BuildingItemViewModel viewModel, VisualElement container) {
            _root = _item.rootVisualElement;
            _container = container;
            _viewModel = viewModel;
            
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
        }

        private VisualElement GetCategoryByType(VisualElement container, StructureType type) {
            var typeName = type.ToString();
            
            return container.Q<VisualElement>(typeName);
        }

        private void SetProps() {
            _name = _root.Q<Label>("Name");
            _incomeLabel = _root.Q<Label>("Income");
            _frequencyLabel = _root.Q<Label>("Frequency");
            _upgradeButton = _root.Q<Button>("UpgradeButton");
            _stabilityLabel = _root.Q<Label>("Stability");
            _stabilityMultiplierLabel = _root.Q<Label>("StabilityMultiplier");
            _multiplierLabel = _root.Q<Label>("Multiplier");
            _criticalChanceLabel = _root.Q<Label>("CriticalChance");
            _criticalMultiplierLabel = _root.Q<Label>("CriticalMultiplier");
            
            _upgradeButton.clicked += () => _viewModel.Upgrade();
            
            _name.text = _viewModel.Name;
        }

    }
}