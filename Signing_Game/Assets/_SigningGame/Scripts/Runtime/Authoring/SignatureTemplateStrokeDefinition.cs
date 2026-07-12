using System;
using System.Collections.Generic;
using UnityEngine;

namespace Authoring {
    [Serializable]
    public sealed class SignatureTemplateStrokeDefinition {
        [SerializeField] private string _id;

        [SerializeField] private List<SignatureCorridorNodeDefinition> _nodes = new();

        [SerializeField]
        private bool _required = true;

        [SerializeField, Min(0f)]
        private float _importance = 1f;

        [SerializeField, Range(0f, 1f)]
        private float _minimumCoverage = 0.8f;

        [SerializeField]
        private bool _allowReverseDirection;

        [SerializeField, Range(0f, 1f)]
        private float _directionImportance = 1f;

        public string Id => _id;
        public IReadOnlyList<SignatureCorridorNodeDefinition> Nodes => _nodes;

        public bool Required => _required;
        public float Importance => _importance;
        public float MinimumCoverage => _minimumCoverage;
        public bool AllowReverseDirection => _allowReverseDirection;
        public float DirectionImportance => _directionImportance;
    }
}