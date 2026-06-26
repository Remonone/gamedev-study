using R3;

namespace Views.Models {
    public class BuildingShopTabViewModel {
        public ReactiveProperty<string> SelectedCategory = new(string.Empty);

        public void RequestInitialState() {
        }

        public void SelectCategory(string categoryName) {
            SelectedCategory.Value = categoryName;
        }
    }
}
