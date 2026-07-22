using System;
using System.Collections.Generic;
using Contracts;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Data.Lease {
    public class AddressableAssetLease<T> : IAssetLease<T> where T : UnityEngine.Object {
        private AsyncOperationHandle<T> _handle;
        private bool _disposed;

        public AddressableAssetLease(AsyncOperationHandle<T> handle) {
            if (!handle.IsValid()) throw new ArgumentException("Handle is not valid", nameof(handle));
            _handle = handle;
        }

        public T Asset {
            get {
                if (_disposed) throw new ObjectDisposedException(nameof(AddressableAssetLease<T>));
                return _handle.Result;
            }
        }
        
        public void Dispose() {
            if (_disposed) return;
            _disposed = true;
            if (_handle.IsValid()) Addressables.Release(_handle);
        }
    }

    public class AddressableAssetListLease<T> : IAssetListLease<T> where T : UnityEngine.Object {
        private AsyncOperationHandle<IList<T>> _handle;
        private IReadOnlyList<T> _assets;
        private bool _disposed;

        public AddressableAssetListLease(AsyncOperationHandle<IList<T>> handle) {
            if (!handle.IsValid()) throw new ArgumentException("Handle is not valid", nameof(handle));
            if (handle.Result == null) throw new ArgumentException("Handle result is null", nameof(handle));
            _handle = handle;
            _assets = handle.Result as IReadOnlyList<T> ?? new List<T>(handle.Result);
        }

        public IReadOnlyList<T> Assets {
            get {
                if (_disposed) throw new ObjectDisposedException(nameof(AddressableAssetListLease<T>));
                return _assets;
            }
        }

        public void Dispose() {
            if (_disposed) return;
            _disposed = true;
            _assets = Array.Empty<T>();
            if (_handle.IsValid()) Addressables.Release(_handle);
        }
    }
}
