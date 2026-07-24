namespace Data.Cache {
    public interface ICacheVersionProvider {
        int GetVersion<T>();
    }
}