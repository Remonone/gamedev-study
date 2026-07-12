using UnityEngine;

namespace Authoring {
    [CreateAssetMenu(
        fileName = "SignatureDifficultyProfile",
        menuName = "Game/Signatures/Difficulty Profile")]
    public sealed class SignatureDifficultyProfileDefinition : ScriptableObject
    {
        [SerializeField]
        private string _id;

        [SerializeField, Range(0f, 1f)]
        private float _minimumSimilarity = 0.4f;

        [SerializeField, Min(0.1f)]
        private float _corridorWidthMultiplier = 1f;

        [SerializeField, Min(0f)]
        private float _coverageRequirementMultiplier = 1f;

        [SerializeField, Min(0f)]
        private float _alignmentToleranceMultiplier = 1f;

        [SerializeField]
        private SignatureScoreWeightsDefinition _scoreWeights = new();

        public string Id => _id;
        public float MinimumSimilarity => _minimumSimilarity;

        public float CorridorWidthMultiplier =>
            _corridorWidthMultiplier;

        public float CoverageRequirementMultiplier =>
            _coverageRequirementMultiplier;

        public float AlignmentToleranceMultiplier =>
            _alignmentToleranceMultiplier;

        public SignatureScoreWeightsDefinition ScoreWeights =>
            _scoreWeights;
    }
}