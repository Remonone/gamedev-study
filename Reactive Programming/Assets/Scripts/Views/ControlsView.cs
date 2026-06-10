using R3;
using UnityEngine;
using UnityEngine.UIElements;
using Views.Models;

namespace Views {
     public class ControlsView : MonoBehaviour {
        private Controls _controls;
        [SerializeField] private UIDocument _controlsDocument;
        [SerializeField] private VisualTreeAsset _achievementItemTemplate;

        private Label _documentsLabel;
        private Label _casesLabel;
        private Label _firesLabel;
        private Label _protectionsLabel;
        private Label _curesLabel;
        private Label _archivesLabel;

        private VisualElement _root;

        private BuildingShopTabView _buildingShopTabView;
        private UpgradesTabView _upgradesTabView;
        private AchievementsTabView _achievementsTabView;
        private ArtifactsTabView _artifactsTabView;

        public void Bind(Controls controls) {
            _controls = controls;
            
            _root = _controlsDocument.rootVisualElement;
            
            _documentsLabel = _root.Q<Label>("DocumentsCount");
            _casesLabel = _root.Q<Label>("CasesCount");
            _firesLabel = _root.Q<Label>("FiresCount");
            _protectionsLabel = _root.Q<Label>("ProtectionsCount");
            _curesLabel = _root.Q<Label>("CuresCount");
            _archivesLabel = _root.Q<Label>("ArchivesCount");
            
            _controls.DocumentsCount.Subscribe(count => _documentsLabel.text = $"{count}").AddTo(this);
            _controls.CasesCount.Subscribe(count => _casesLabel.text = $"{count}").AddTo(this);
            _controls.FiresCount.Subscribe(count => _firesLabel.text = $"{count}").AddTo(this);
            _controls.ProtectionsCount.Subscribe(count => _protectionsLabel.text = $"{count}").AddTo(this);
            _controls.CuresCount.Subscribe(count => _curesLabel.text = $"{count}").AddTo(this);
            _controls.ArchivesCount.Subscribe(count => _archivesLabel.text = $"{count}").AddTo(this);

            _buildingShopTabView = new BuildingShopTabView(_root.Q<VisualElement>("BuildingShopTabRoot"));
            _upgradesTabView = new UpgradesTabView(_root.Q<VisualElement>("UpgradesTabRoot"));
            _achievementsTabView = new AchievementsTabView(_root.Q<VisualElement>("AchievementsTabRoot"), ResolveAchievementItemTemplate());
            _artifactsTabView = new ArtifactsTabView(_root.Q<VisualElement>("ArtifactsTabRoot"));

            _buildingShopTabView.Bind(_controls.BuildingShopTab);
            _upgradesTabView.Bind(_controls.UpgradesTab);
            _achievementsTabView.Bind(_controls.AchievementsTab);
            _artifactsTabView.Bind(_controls.ArtifactsTab);
        }

        private VisualTreeAsset ResolveAchievementItemTemplate() {
            if (_achievementItemTemplate != null) {
                return _achievementItemTemplate;
            }

            return Resources.Load<VisualTreeAsset>("UI/AchievementItem");
        }

        private void OnDestroy() {
            _controls?.Dispose();
            _upgradesTabView?.Dispose();
            _achievementsTabView?.Dispose();
            _artifactsTabView?.Dispose();
        }
     }
}
