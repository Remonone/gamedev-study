using System;

namespace Data.Cache {
    public interface ICacheInvalidator {
        void Invalidate<T>();
        void InvalidateAll();
        void Invalidate(Type type);
    }
}