using UnityEngine;

namespace Authoring {
    [CreateAssetMenu(
        fileName = "SignatureProcessingSettings",
        menuName = "Game/Signatures/Processing Settings")]
    public sealed class SignatureProcessingSettingsDefinition : ScriptableObject {
        [SerializeField, Min(0f)] private float _minimumInputPointDistance = 0.002f;

        [SerializeField, Min(2)] private int _minimumUsablePointCountPerStroke = 4;

        [SerializeField, Min(0f)] private float _minimumStrokeLength = 0.01f;

        [SerializeField, Min(4)] private int _resampledPointCountPerStroke = 64;

        [SerializeField, Min(0)] private int _smoothingPasses = 1;

        [SerializeField, Min(16)] private int _maximumInputPointCount = 2048;

        public float MinimumInputPointDistance => _minimumInputPointDistance;

        public int MinimumUsablePointCountPerStroke => _minimumUsablePointCountPerStroke;

        public float MinimumStrokeLength => _minimumStrokeLength;

        public int ResampledPointCountPerStroke => _resampledPointCountPerStroke;

        public int SmoothingPasses => _smoothingPasses;

        public int MaximumInputPointCount => _maximumInputPointCount;
    }
}