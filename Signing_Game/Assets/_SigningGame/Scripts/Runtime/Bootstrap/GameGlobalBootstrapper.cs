using Services.Locator;
using UnityEngine;

namespace Bootstrap {
    public class GameGlobalBootstrapper : Bootstrapper {
        [SerializeField] private bool _dontDestroyOnLoad = true;
        
        protected override void Configure() {
            Container.ConfigureAsGlobal(_dontDestroyOnLoad);
        }
    }
}