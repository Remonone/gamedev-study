using UnityEngine;
using Views.Models;

using Bases;
using Bus;
using Types.Events;

namespace Views {
    public class AreaWatcherView : MonoBehaviour {
    
        private AreaClickerViewModel _areaClickerViewModel;

        private Listener<ClickEvent> _clickListener;

        public void Init(AreaClickerViewModel areaClickerViewModel) {
            _areaClickerViewModel = areaClickerViewModel;
            
            _clickListener = new Listener<ClickEvent>(HandleClick);
            EventBus<ClickEvent>.Register(_clickListener, this);
        }

        private void HandleClick(ClickEvent e){
            if (ReferenceEquals(e.ClickedObject, null)) return;
            if(e.ClickedObject.TryGetComponent(out IStructure structure)) {
                _areaClickerViewModel.Click(structure.Type);
            }
        }
    }
}
