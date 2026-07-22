using System;
using Cysharp.Threading.Tasks;
using Services.Locator;
using UnityEngine;

namespace Services {
    public interface IService : IDisposable { }

    public interface IPreInitialize {
        UniTask PreInitializeAsync(IServiceScope scope);
    }

    public interface IInitialize {
        UniTask InitializeAsync(IServiceScope scope);
    }

    public interface IPostInitialize {
        UniTask PostInitializeAsync(IServiceScope scope);
    }
}
