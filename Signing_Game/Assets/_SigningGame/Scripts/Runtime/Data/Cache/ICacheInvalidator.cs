namespace Data.Cache {
    public interface ICacheInvalidator {
        void Invalidate<T>();
    }
}