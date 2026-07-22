using System;
using System.Collections.Generic;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Lease;
using Exceptions;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace Services {
    public class AddressablesService : IService, IAssetProvider {
        public void Dispose() {
            
        }

        public async UniTask<IAssetLease<T>> LoadAsync<T>(AssetReferenceT<T> reference) where T : Object {
            if (reference == null) throw new ArgumentNullException(nameof(reference));
            
            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(reference);
            
            try {
                await handle;
                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null) {
                    throw CreateLoadException(reference.RuntimeKey, handle.OperationException);
                }

                return new AddressableAssetLease<T>(handle);
            } catch {
                if (handle.IsValid()) {
                    Addressables.Release(handle);
                }

                throw;
            }
        }

        public async UniTask<IAssetListLease<T>> LoadAssetsByLabelAsync<T>(string label) where T : Object {
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("Label cannot be empty.", nameof(label));

            AsyncOperationHandle<IList<T>> handle = Addressables.LoadAssetsAsync<T>(label, (Action<T>)null);

            try {
                await handle;
                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null) {
                    throw CreateLoadException(label, handle.OperationException);
                }

                return new AddressableAssetListLease<T>(handle);
            } catch {
                if (handle.IsValid()) {
                    Addressables.Release(handle);
                }

                throw;
            }
        }

        public async UniTask<IInstanceLease> InstantiateAsync(AssetReference instanceReference, Transform parent = null, bool worldPositionStays = false) {
            if (instanceReference == null) throw new ArgumentNullException(nameof(instanceReference));
            
            AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync(instanceReference, parent, worldPositionStays, trackHandle: true);

            try {
                await handle;

                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null) {
                    throw CreateLoadException(instanceReference.RuntimeKey, handle.OperationException);
                }

                return new AddressableInstanceLease(handle);
            } catch {
                if (handle.IsValid()) {
                    throw;
                }

                if (handle.Status == AsyncOperationStatus.Succeeded &&
                    handle.Result != null) {
                    Addressables.ReleaseInstance(handle);
                    throw;
                }

                Addressables.Release(handle);
                throw;
            }
        }
        
        
        private static Exception CreateLoadException(
            object runtimeKey,
            Exception innerException)
        {
            return new AssetLoadException(
                $"Failed to load Addressable asset '{runtimeKey}'.",
                innerException);
        }
    }
}
