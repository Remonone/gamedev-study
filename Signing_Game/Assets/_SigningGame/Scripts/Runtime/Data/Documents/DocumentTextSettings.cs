using UnityEngine;

namespace Data.Documents {
    [CreateAssetMenu(
        fileName = "FakeDocumentSettings",
        menuName = "Game/Documents/Document Settings")]
    public sealed class DocumentTextSettings : ScriptableObject
    {
        [Header("Glyphs")]
        [Tooltip("Each element represents a glyph in the document.")]
        [SerializeField]
        private string[] _glyphs =
        {
            "╱", "╲", "•", "○", "◇",
            "△", "⌁", "┆", "┐", "└"
        };

        [Header("Document structure")]
        [SerializeField] private Vector2Int _paragraphCount = new(4, 9);

        [SerializeField]
        private Vector2Int _sentencesPerParagraph = new(2, 5);

        [SerializeField]
        private Vector2Int _wordsPerSentence = new(5, 15);

        [SerializeField]
        private Vector2Int _titleWords = new(3, 7);

        [SerializeField]
        private Vector2Int _headingWords = new(2, 6);

        [SerializeField, Min(0)]
        private int _minimumParagraphsBetweenHeadings = 2;

        [Header("Font sizes")]
        [SerializeField] private Vector2Int _titleFontSize = new(34, 42);
        [SerializeField] private Vector2Int _headingFontSize = new(27, 31);
        [SerializeField] private Vector2Int _bodyFontSize = new(21, 24);
        [SerializeField] private Vector2Int _footnoteFontSize = new(16, 19);

        [Header("Probabilities")]
        [SerializeField, Range(0f, 1f)]
        private float _titleProbability = 0.85f;

        [SerializeField, Range(0f, 1f)]
        private float _headingProbability = 0.3f;

        [SerializeField, Range(0f, 1f)]
        private float _footnoteProbability = 0.35f;

        [SerializeField, Range(0f, 1f)]
        private float _commaProbability = 0.12f;

        [SerializeField, Range(0f, 1f)]
        private float _semicolonProbability = 0.015f;

        [Header("Word length distribution")]
        [Tooltip("Index of the element represents the word length.")]
        [SerializeField]
        private int[] _wordLengthWeights = {
            0,
            2,  // 1 symbol
            6,  // 2
            13, // 3
            20, // 4
            22, // 5
            17, // 6
            10, // 7
            6,  // 8
            3,  // 9
            1   // 10
        };

        public string[] Glyphs => _glyphs;

        public Vector2Int ParagraphCount => _paragraphCount;
        public Vector2Int SentencesPerParagraph => _sentencesPerParagraph;
        public Vector2Int WordsPerSentence => _wordsPerSentence;
        public Vector2Int TitleWords => _titleWords;
        public Vector2Int HeadingWords => _headingWords;

        public int MinimumParagraphsBetweenHeadings =>
            _minimumParagraphsBetweenHeadings;

        public Vector2Int TitleFontSize => _titleFontSize;
        public Vector2Int HeadingFontSize => _headingFontSize;
        public Vector2Int BodyFontSize => _bodyFontSize;
        public Vector2Int FootnoteFontSize => _footnoteFontSize;

        public float TitleProbability => _titleProbability;
        public float HeadingProbability => _headingProbability;
        public float FootnoteProbability => _footnoteProbability;
        public float CommaProbability => _commaProbability;
        public float SemicolonProbability => _semicolonProbability;

        public int[] WordLengthWeights => _wordLengthWeights;
    }
}