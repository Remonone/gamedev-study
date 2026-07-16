namespace Bootstrap {
    public class SceneBootstrapper : Bootstrapper {
        protected override void Configure() {
            Container.ConfigureForScene();
        }
    }
}