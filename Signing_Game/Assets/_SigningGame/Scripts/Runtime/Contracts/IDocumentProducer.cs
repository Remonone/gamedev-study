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
        DocumentEvaluationInputs Resolve(SignatureDifficultyContext difficulty);
    }

    public readonly struct SignatureDifficultyContext {
        public SignatureDifficultyRules ConfiguredDifficulty { get; }
        public SignatureDifficultyRules EffectiveDifficulty { get; }

        public SignatureDifficultyContext(
            SignatureDifficultyRules configuredDifficulty,
            SignatureDifficultyRules effectiveDifficulty) {
            ConfiguredDifficulty = configuredDifficulty ?? throw new ArgumentNullException(nameof(configuredDifficulty));
            EffectiveDifficulty = effectiveDifficulty ?? throw new ArgumentNullException(nameof(effectiveDifficulty));
        }
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
