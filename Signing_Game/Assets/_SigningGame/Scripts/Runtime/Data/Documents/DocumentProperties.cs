using System;
using System.Collections.Generic;

namespace Data.Documents {
    public class DocumentProperties : IDocumentContext {
        private readonly Dictionary<Type, object> _behaviors = new Dictionary<Type, object>();

        public void AddBehavior<T>(T behavior) where T : notnull {
            _behaviors[typeof(T)] = behavior ?? throw new ArgumentNullException(nameof(behavior));
        }
        
        public DocumentProperties() {}
        
        public void GetBehavior<T>(out T behavior) where T : notnull {
            if (_behaviors.ContainsKey(typeof(T))) {
                behavior = (T)_behaviors[typeof(T)];
            }
            else {
                throw new InvalidOperationException($"Behavior of type {typeof(T)} not found.");
            }
        }
    }
}