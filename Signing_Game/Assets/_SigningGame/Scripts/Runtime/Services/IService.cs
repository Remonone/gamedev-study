using System;
using Services.Locator;
using UnityEngine;

namespace Services {
    public interface IService : IDisposable { }

    public interface IPreInitialize {
        Awaitable PreInitializeAsync(IServiceScope scope);
    }

    public interface IInitialize {
        Awaitable InitializeAsync(IServiceScope scope);
    }

    public interface IPostInitialize {
        Awaitable PostInitializeAsync(IServiceScope scope);
    }
}
