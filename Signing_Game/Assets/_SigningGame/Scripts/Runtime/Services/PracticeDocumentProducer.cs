using System;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Documents;
using Data.Results;
using Data.Rules;
using R3;
using Services.Locator;

namespace Services {
    public sealed class PracticeDocumentProducer : IService, IInitialize, IDocumentProducer {
        private ResearchService _research;
        public int Priority => 300;
        public Observable<Unit> OffersChanged => _research.DocumentOffersChanged;

        public UniTask InitializeAsync(IServiceScope scope) {
            _research = scope.Get<ResearchService>();
            return UniTask.CompletedTask;
        }

        public bool TryPeekOffer(out DocumentOffer offer) => _research.TryPeekPendingDocument(out offer);

        public bool TryProduce(DocumentOfferKey offerKey, out IDocumentSession session) {
            session = null;
            if (offerKey.Kind != DocumentKind.Practice ||
                !_research.TryClaimPending(offerKey.DomainId, out ResearchService.PracticeDocumentClaim claim)) return false;
            session = new PracticeDocumentSession(_research, claim);
            return true;
        }

        public void Dispose() { }

        private sealed class PracticeDocumentSession : IDocumentSession {
            private readonly ResearchService _research;
            private readonly ResearchService.PracticeDocumentClaim _claim;
            private readonly IDocumentEvaluationPolicy _policy;
            private bool _finished;

            public DocumentKind Kind => DocumentKind.Practice;
            public IDocumentEvaluationPolicy EvaluationPolicy => _policy;

            public PracticeDocumentSession(ResearchService research, ResearchService.PracticeDocumentClaim claim) {
                _research = research;
                _claim = claim;
                _policy = new PracticeEvaluationPolicy(claim.SignatureThreshold);
            }

            public bool TryProcess(SignatureEvaluationResult result) {
                if (result == null) throw new ArgumentNullException(nameof(result));
                if (_finished) return false;
                bool processed = _research.TryProcessClaim(_claim, result);
                if (processed) _finished = true;
                return processed;
            }

            public void Dispose() {
                if (_finished) return;
                _finished = true;
                _research.TryReleaseClaim(_claim);
            }
        }

        private sealed class PracticeEvaluationPolicy : IDocumentEvaluationPolicy {
            private readonly float _threshold;
            public PracticeEvaluationPolicy(float threshold) => _threshold = threshold;

            public DocumentEvaluationInputs Resolve(SignatureDifficultyContext difficulty) {
                SignatureDifficultyRules baseDifficulty = difficulty.ConfiguredDifficulty;
                if (baseDifficulty == null) throw new ArgumentNullException(nameof(baseDifficulty));
                return new DocumentEvaluationInputs(
                    baseDifficulty with { MinimumSimilarity = _threshold },
                    SignatureRuleModifiers.None);
            }
        }
    }
}
