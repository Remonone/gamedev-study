using System;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Documents;
using Data.Results;
using Data.Rules;
using R3;
using Services.Locator;

namespace Services {
    public sealed class BillDocumentProducer : IService, IInitialize, IDocumentProducer {
        private BillService _bills;

        public int Priority => 300;
        public Observable<Unit> OffersChanged => _bills.DocumentOffersChanged;

        public UniTask InitializeAsync(IServiceScope scope) {
            _bills = scope.Get<BillService>();
            return UniTask.CompletedTask;
        }

        public bool TryPeekOffer(out DocumentOffer offer) {
            return _bills.TryPeekPendingDocument(out offer);
        }

        public bool TryProduce(DocumentOfferKey offerKey, out IDocumentSession session) {
            session = null;
            if (offerKey.Kind != DocumentKind.Bill ||
                !_bills.TryClaimPending(offerKey.DomainId, out BillService.BillDocumentClaim claim)) {
                return false;
            }

            session = new BillDocumentSession(_bills, claim);
            return true;
        }

        public void Dispose() { }

        private sealed class BillDocumentSession : IDocumentSession {
            private readonly BillService _bills;
            private readonly BillService.BillDocumentClaim _claim;
            private readonly IDocumentEvaluationPolicy _policy;
            private bool _finished;

            public DocumentKind Kind => DocumentKind.Bill;
            public IDocumentEvaluationPolicy EvaluationPolicy => _policy;

            public BillDocumentSession(BillService bills, BillService.BillDocumentClaim claim) {
                _bills = bills;
                _claim = claim;
                _policy = new BillEvaluationPolicy(claim.SignatureThreshold);
            }

            public bool TryProcess(SignatureEvaluationResult result) {
                if (result == null) throw new ArgumentNullException(nameof(result));
                if (_finished) return false;
                bool processed = _bills.TryProcessClaim(_claim, result);
                if (processed) _finished = true;
                return processed;
            }

            public void Dispose() {
                if (_finished) return;
                _finished = true;
                _bills.TryReleaseClaim(_claim);
            }
        }

        private sealed class BillEvaluationPolicy : IDocumentEvaluationPolicy {
            private readonly float _threshold;

            public BillEvaluationPolicy(float threshold) {
                _threshold = threshold;
            }

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
