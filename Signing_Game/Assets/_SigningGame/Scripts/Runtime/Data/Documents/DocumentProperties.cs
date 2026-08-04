using System;
using System.Collections.Generic;
using Contracts;

namespace Data.Documents {
    public class DocumentProperties : IDocumentContext {
        private readonly Dictionary<Type, object> _behaviors = new Dictionary<Type, object>();
        private IDocumentSession _session;
        private bool _sessionTaken;

        public DocumentProperties(IDocumentSession session) {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public void AddBehavior<T>(T behavior) where T : notnull {
            _behaviors[typeof(T)] = behavior ?? throw new ArgumentNullException(nameof(behavior));
        }

        public void GetBehavior<T>(out T behavior) where T : notnull {
            if (_behaviors.TryGetValue(typeof(T), out object value)) {
                behavior = (T)value;
            }
            else {
                throw new InvalidOperationException($"Behavior of type {typeof(T)} not found.");
            }
        }

        public IDocumentSession TakeSession() {
            if (_sessionTaken || _session == null) {
                throw new InvalidOperationException("The document session has already been transferred.");
            }

            _sessionTaken = true;
            IDocumentSession session = _session;
            _session = null;
            return session;
        }

        public void Dispose() {
            _session?.Dispose();
            _session = null;
            _behaviors.Clear();
        }
    }
}
