using System.Collections.Generic;

namespace Utils.Text.Generator {
    public sealed class GeneratedDocument {
        public IReadOnlyList<GeneratedDocumentBlock> Blocks { get; }

        public GeneratedDocument(IReadOnlyList<GeneratedDocumentBlock> blocks) {
            Blocks = blocks;
        }
    }
}