using System.Collections.Generic;

namespace Data.Templates {
    public class SignatureTemplateVariant {
        public string Id { get; }
        public IReadOnlyList<SignatureTemplateStroke> Strokes { get; }
    }
}