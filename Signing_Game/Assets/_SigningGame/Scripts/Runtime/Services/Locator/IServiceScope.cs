namespace Services.Locator {
    public interface IServiceScope {
        T Get<T>() where T : class;
        
        bool TryGet<T>(out T service) where T : class;
        
        ServiceLocator Container { get; }
    }
}