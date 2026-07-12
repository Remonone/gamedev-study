using System;
using UnityEngine;

namespace Authoring {
    [Serializable]
    public sealed class SignatureScoreWeightsDefinition {
        [SerializeField, Min(0f)]
        private float _corridorFit = 0.55f;

        [SerializeField, Min(0f)]
        private float _coverage = 0.25f;

        [SerializeField, Min(0f)]
        private float _direction = 0.15f;

        [SerializeField, Min(0f)]
        private float _strokeStructure = 0.05f;

        public float CorridorFit => _corridorFit;
        public float Coverage => _coverage;
        public float Direction => _direction;
        public float StrokeStructure => _strokeStructure;
    }
}