namespace Services.Locator {
    public interface IServiceScope {
        T Get<T>(int index = 0) where T : class;
        
        bool TryGet<T>(out T service, int index = 0) where T : class;
        
        ServiceLocator Container { get; }
    }
}
