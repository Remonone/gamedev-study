using Contracts;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Data.Lease {
    public class AddressableInstanceLease : IInstanceLease {
        private AsyncOperationHandle<GameObject> _handle;
        private bool _disposed;
        
        public AddressableInstanceLease(AsyncOperationHandle<GameObject> handle) {
            if (!handle.IsValid()) throw new System.ArgumentException("Handle is not valid", nameof(handle));
            _handle = handle;
        }

        public GameObject Instance {
            get {
                if (_disposed) throw new System.ObjectDisposedException(nameof(AddressableInstanceLease));
                return _handle.Result;
            }
        }

        public void Dispose() {
            if (_disposed) return;
            _disposed = true;
            Addressables.Release(_handle);
        }
    }
}