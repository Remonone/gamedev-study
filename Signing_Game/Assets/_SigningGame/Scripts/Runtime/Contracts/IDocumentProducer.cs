using System;
using Data.Results;
using Data.Rules;

namespace Contracts {
    public interface IDocumentProducer {
        int Priority { get; }
        bool TryProduce(out IDocumentSession session);
    }

    public interface IDocumentSession : IDisposable {
        IDocumentEvaluationPolicy EvaluationPolicy { get; }
        bool TryProcess(SignatureEvaluationResult result);
    }

    public interface IDocumentEvaluationPolicy {
        DocumentEvaluationInputs Resolve(
            SignatureDifficultyRules baseDifficulty,
            SignatureRuleModifiers playerModifiers);
    }

    public readonly struct DocumentEvaluationInputs {
        public SignatureDifficultyRules Difficulty { get; }
        public SignatureRuleModifiers Modifiers { get; }

        public DocumentEvaluationInputs(
            SignatureDifficultyRules difficulty,
            SignatureRuleModifiers modifiers) {
            Difficulty = difficulty ?? throw new ArgumentNullException(nameof(difficulty));
            Modifiers = modifiers;
        }
    }
}
