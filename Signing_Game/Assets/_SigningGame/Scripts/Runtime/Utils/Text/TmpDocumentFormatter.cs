using System.Text;
using Utils.Text.Generator;

namespace Utils.Text {
    public static class TmpDocumentFormatter {
        public static string Format(GeneratedDocument document) {
            var builder = new StringBuilder();

            for (int i = 0; i < document.Blocks.Count; i++) {
                GeneratedDocumentBlock block =
                    document.Blocks[i];

                AppendBlock(builder, block);

                if (i < document.Blocks.Count - 1) {
                    builder.Append(
                        block.Type == DocumentBlockType.Title
                            ? "\n\n\n"
                            : "\n\n");
                }
            }

            return builder.ToString();
        }

        private static void AppendBlock(StringBuilder builder, GeneratedDocumentBlock block) {
            switch (block.Type) {
                case DocumentBlockType.Title:
                    builder.Append("<align=center>");
                    builder.Append("<size=");
                    builder.Append(block.FontSize);
                    builder.Append("><b><noparse>");
                    builder.Append(block.Text);
                    builder.Append(
                        "</noparse></b></size></align>");
                    break;

                case DocumentBlockType.Heading:
                    builder.Append("<size=");
                    builder.Append(block.FontSize);
                    builder.Append("><b><noparse>");
                    builder.Append(block.Text);
                    builder.Append("</noparse></b></size>");
                    break;

                case DocumentBlockType.Body:
                    builder.Append("<size=");
                    builder.Append(block.FontSize);
                    builder.Append("><noparse>");
                    builder.Append(block.Text);
                    builder.Append("</noparse></size>");
                    break;

                case DocumentBlockType.Footnote:
                    builder.Append("<size=");
                    builder.Append(block.FontSize);
                    builder.Append("><i><noparse>");
                    builder.Append(block.Text);
                    builder.Append("</noparse></i></size>");
                    break;
            }
        }
    }
}