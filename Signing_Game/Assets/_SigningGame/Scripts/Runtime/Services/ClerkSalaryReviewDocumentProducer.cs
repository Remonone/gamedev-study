using System;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Results;
using Data.Rules;
using Services.Locator;

namespace Services {
    public sealed class ClerkSalaryReviewDocumentProducer : IService, IInitialize, IDocumentProducer {
        private static readonly IDocumentEvaluationPolicy Policy = new SalaryReviewDocumentEvaluationPolicy();
        private OfficeService _office;

        public int Priority => 200;

        public UniTask InitializeAsync(IServiceScope scope) {
            _office = scope.Get<OfficeService>();
            return UniTask.CompletedTask;
        }

        public bool TryProduce(out IDocumentSession session) {
            session = null;
            if (!_office.TryClaimPendingSalaryReview(out OfficeService.SalaryReviewDocumentClaim claim)) {
                return false;
            }

            session = new SalaryReviewDocumentSession(_office, claim);
            return true;
        }

        public void Dispose() { }

        private sealed class SalaryReviewDocumentEvaluationPolicy : IDocumentEvaluationPolicy {
            public DocumentEvaluationInputs Resolve(
                SignatureDifficultyRules baseDifficulty,
                SignatureRuleModifiers playerModifiers) {
                return new DocumentEvaluationInputs(baseDifficulty, SignatureRuleModifiers.None);
            }
        }

        private sealed class SalaryReviewDocumentSession : IDocumentSession {
            private readonly OfficeService _office;
            private readonly OfficeService.SalaryReviewDocumentClaim _claim;
            private bool _finished;

            public IDocumentEvaluationPolicy EvaluationPolicy => Policy;

            public SalaryReviewDocumentSession(
                OfficeService office,
                OfficeService.SalaryReviewDocumentClaim claim) {
                _office = office;
                _claim = claim;
            }

            public bool TryProcess(SignatureEvaluationResult result) {
                if (result == null) throw new ArgumentNullException(nameof(result));
                if (_finished) return false;
                bool completed = _office.TryCompletePendingSalaryReview(_claim, result);
                if (completed) _finished = true;
                return completed;
            }

            public void Dispose() {
                if (_finished) return;
                _finished = true;
                _office.TryReleasePendingSalaryReview(_claim);
            }
        }
    }
}
