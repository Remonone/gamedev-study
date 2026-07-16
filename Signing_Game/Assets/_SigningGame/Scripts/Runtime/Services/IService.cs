using System;
using Services.Locator;
using UnityEngine;

namespace Services {
    public interface IService : IDisposable { }

    public interface IPreInitialize {
        Awaitable PreInitializeAsync(ServiceLocator container);
    }

    public interface IInitialize {
        Awaitable InitializeAsync(ServiceLocator container);
    }

    public interface IPostInitialize {
        Awaitable PostInitializeAsync(ServiceLocator container);
    }
}
