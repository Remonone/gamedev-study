using UnityEngine;

namespace Types.QTE {
    public sealed class QteSpawnMarker : MonoBehaviour {
        [SerializeField, Tooltip("World-space offset from this marker object's position where the QTE prefab should spawn.")]
        private Vector3 _offset;

        public Vector3 Offset => _offset;
        public Vector3 SpawnPosition => transform.position + _offset;
    }
}
