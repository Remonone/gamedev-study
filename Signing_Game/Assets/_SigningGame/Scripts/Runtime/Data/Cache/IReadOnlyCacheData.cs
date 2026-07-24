namespace Data.Cache {
    public interface IReadOnlyCacheData<out TData> {
        TData Value { get; }
    }
}