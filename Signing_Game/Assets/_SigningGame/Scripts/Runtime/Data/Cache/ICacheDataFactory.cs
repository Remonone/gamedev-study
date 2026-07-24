namespace Data.Cache {
    public interface ICacheDataFactory {
        CachedData<T> Create<T>();
    }
}