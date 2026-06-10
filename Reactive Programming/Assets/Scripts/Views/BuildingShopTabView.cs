using UnityEngine.UIElements;
using Views.Models;

namespace Views {
    public class BuildingShopTabView {
        private readonly VisualElement _root;

        public BuildingShopTabView(VisualElement root) {
            _root = root;
        }

        public void Bind(BuildingShopTabViewModel viewModel) {
            if (_root == null || viewModel == null) {
                return;
            }

            viewModel.RequestInitialState();
        }
    }
}
