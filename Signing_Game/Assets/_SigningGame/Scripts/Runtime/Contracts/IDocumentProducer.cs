using System;
using Data.Documents;
using Data.Results;
using Data.Rules;
using R3;

namespace Contracts {
    public interface IDocumentProducer {
        int Priority { get; }
        Observable<Unit> OffersChanged { get; }
        bool TryPeekOffer(out DocumentOffer offer);
        bool TryProduce(DocumentOfferKey offerKey, out IDocumentSession session);
    }

    public interface IDocumentSession : IDisposable {
        DocumentKind Kind { get; }
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
