namespace Utils.Text.Generator {
    public sealed class GeneratedDocumentBlock {
        public DocumentBlockType Type { get; }
        public string Text { get; }
        public int FontSize { get; }

        public GeneratedDocumentBlock(DocumentBlockType type, string text, int fontSize) {
            Type = type;
            Text = text;
            FontSize = fontSize;
        }
    }
}