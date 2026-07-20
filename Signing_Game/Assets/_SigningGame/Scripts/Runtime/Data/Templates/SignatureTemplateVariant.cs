using System;
using System.Collections.Generic;

namespace Data.Templates {
    public class SignatureTemplateVariant {
        public string Id { get; }
        public IReadOnlyList<SignatureTemplateStroke> Strokes { get; }

        public SignatureTemplateVariant(string id, IReadOnlyList<SignatureTemplateStroke> strokes) {
            Id = id;
            if (strokes == null) throw new ArgumentNullException(nameof(strokes));
            var snapshot = new SignatureTemplateStroke[strokes.Count];
            for (int i = 0; i < snapshot.Length; i++) snapshot[i] = strokes[i];
            Strokes = Array.AsReadOnly(snapshot);
        }
    }
}
