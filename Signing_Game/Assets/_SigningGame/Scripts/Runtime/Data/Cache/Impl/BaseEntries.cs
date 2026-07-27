using UnityEngine;

namespace Data.Cache {
    
    public class BaseEntries<T> : ScriptableObject where T : struct {
        public T Value;    
    }
}