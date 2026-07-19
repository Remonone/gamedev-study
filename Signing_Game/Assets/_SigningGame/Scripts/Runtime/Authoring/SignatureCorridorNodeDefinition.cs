using System;
using UnityEngine;

namespace Authoring {
    [Serializable]
    public sealed class SignatureCorridorNodeDefinition {
        [SerializeField] private Vector2 _position;
        [SerializeField, Min(0.001f)] private float _radius = 0.05f;
        [SerializeField, Min(0f)] private float _importance = 1f;
        public Vector2 Position => _position;
        public float Radius => _radius;
        public float Importance => _importance;
    }
}
