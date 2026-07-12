using System;
using UnityEngine;

namespace Authoring {
    [Serializable]
    public sealed class SignatureAlignmentDefinition {
        [SerializeField, Range(0f, 0.5f)] private float _maximumTranslation = 0.1f;

        [SerializeField, Min(0.01f)] private float _minimumScale = 0.85f;

        [SerializeField, Min(0.01f)] private float _maximumScale = 1.15f;

        [SerializeField, Range(0f, 45f)] private float _maximumRotationDegrees = 5f;

        public float MaximumTranslation => _maximumTranslation;
        public float MinimumScale => _minimumScale;
        public float MaximumScale => _maximumScale;
        public float MaximumRotationDegrees => _maximumRotationDegrees;
    }
}