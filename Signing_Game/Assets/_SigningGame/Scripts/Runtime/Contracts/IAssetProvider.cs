using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Contracts {
    public interface IAssetProvider {
        UniTask<IAssetLease<T>> LoadAsync<T>(AssetReferenceT<T> reference) where T : UnityEngine.Object;
        UniTask<IAssetListLease<T>> LoadAssetsByLabelAsync<T>(string label) where T : UnityEngine.Object;
        UniTask<IInstanceLease> InstantiateAsync(AssetReference instanceReference, Transform parent = null, bool worldPositionStays = false);
    }
    
    public interface IAssetLease<out T> : IDisposable where T : UnityEngine.Object {
        T Asset { get; }
    }

    public interface IAssetListLease<out T> : IDisposable where T : UnityEngine.Object {
        IReadOnlyList<T> Assets { get; }
    }

    public interface IInstanceLease : IDisposable {
        GameObject Instance { get; }
    }
}
