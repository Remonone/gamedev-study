using System;
using Services.Locator;

namespace SigningGame.Tests.EditMode {
    public class TestUtils {
        public static FakeScope CreateFakeScope(params object[] services) {
            var scope = new FakeScope();
            foreach (var service in services) scope.Add(service);
            return scope;
        }
    }

    public sealed class FakeScope : IServiceScope {
        private readonly System.Collections.Generic.List<object> _services = new();

        public void Add(object service) => _services.Add(service);

        public T Get<T>() where T : class {
            if (TryGet(out T service)) return service;
            throw new ArgumentException($"Service assignable to {typeof(T)} not registered.");
        }

        public bool TryGet<T>(out T service) where T : class {
            service = null;
            foreach (object candidate in _services) {
                if (candidate is not T match) continue;
                if (service != null)
                    throw new InvalidOperationException($"Multiple services are assignable to {typeof(T)}.");
                service = match;
            }
            return service != null;
        }
    }
}
