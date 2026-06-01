using Components.Instances;
using Player;
using UnityEngine;
using Views;
using Views.Models;

namespace Components {
    public class GameServiceInstaller : ServiceInstaller {
        
        [SerializeField] private AreaWatcherView _areaWatcherView;

        protected override void InstallServices() {
            ServiceLocator.Instance.RegisterService(new StructureClickService());
            ServiceLocator.Instance.RegisterService(new Storage());
            InitViews();
        }

        private void InitViews() {
            var areaClickerViewModel = new AreaClickerViewModel();
            _areaWatcherView.Init(areaClickerViewModel);
        }
    }
}