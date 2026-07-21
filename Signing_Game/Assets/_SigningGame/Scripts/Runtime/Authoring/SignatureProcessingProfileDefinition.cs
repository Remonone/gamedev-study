using Data.Rules;
using Exceptions;
using UnityEngine;

namespace Authoring {
    [CreateAssetMenu(fileName = "SignatureProcessingProfile", menuName = "Game/Signatures/Processing Profile")]
    public sealed class SignatureProcessingProfileDefinition : ScriptableObject {
        [SerializeField] private string _id;
        [SerializeField, Min(0f)] private float _minimumInputPointDistance = 0.002f;
        [SerializeField, Min(2)] private int _minimumUsablePointCountPerStroke = 4;
        [SerializeField, Min(0f)] private float _minimumStrokeLength = 0.01f;
        [SerializeField, Min(2)] private int _resampledPointCountPerStroke = 64;
        [SerializeField, Min(0)] private int _smoothingPasses = 1;
        [SerializeField, Min(1)] private int _maximumInputPointCount = 2048;

        public string Id => _id;
        public float MinimumInputPointDistance => _minimumInputPointDistance;
        public int MinimumUsablePointCountPerStroke => _minimumUsablePointCountPerStroke;
        public float MinimumStrokeLength => _minimumStrokeLength;
        public int ResampledPointCountPerStroke => _resampledPointCountPerStroke;
        public int SmoothingPasses => _smoothingPasses;
        public int MaximumInputPointCount => _maximumInputPointCount;

        public SignatureProcessingRules ToRules() {
            if (string.IsNullOrWhiteSpace(_id)) throw new SignaturePresetConfigurationException("Processing profile Id is required.");
            if (!Finite(_minimumInputPointDistance) || _minimumInputPointDistance < 0f)
                throw new SignaturePresetConfigurationException("Minimum input point distance must be finite and nonnegative.");
            if (_minimumUsablePointCountPerStroke < 2)
                throw new SignaturePresetConfigurationException("Minimum usable point count must be at least two.");
            if (!Finite(_minimumStrokeLength) || _minimumStrokeLength < 0f)
                throw new SignaturePresetConfigurationException("Minimum stroke length must be finite and nonnegative.");
            if (_resampledPointCountPerStroke < 2)
                throw new SignaturePresetConfigurationException("Resampled point count must be at least two.");
            if (_smoothingPasses < 0)
                throw new SignaturePresetConfigurationException("Smoothing passes cannot be negative.");
            if (_maximumInputPointCount < _minimumUsablePointCountPerStroke)
                throw new SignaturePresetConfigurationException("Maximum input point count must be at least the minimum usable count.");
            return new SignatureProcessingRules(_minimumInputPointDistance, _minimumUsablePointCountPerStroke,
                _minimumStrokeLength, _resampledPointCountPerStroke, _smoothingPasses, _maximumInputPointCount);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
