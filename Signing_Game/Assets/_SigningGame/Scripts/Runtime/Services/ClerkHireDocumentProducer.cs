using System;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Results;
using Data.Rules;
using Services.Locator;

namespace Services {
    public sealed class ClerkHireDocumentProducer : IService, IInitialize, IDocumentProducer {
        private static readonly IDocumentEvaluationPolicy Policy = new ClerkHireDocumentEvaluationPolicy();
        private OfficeService _office;

        public int Priority => 200;

        public UniTask InitializeAsync(IServiceScope scope) {
            _office = scope.Get<OfficeService>();
            return UniTask.CompletedTask;
        }

        public bool TryProduce(out IDocumentSession session) {
            session = null;
            if (!_office.TryClaimPendingClerkHire(out OfficeService.ClerkHireDocumentClaim claim)) return false;
            session = new ClerkHireDocumentSession(_office, claim);
            return true;
        }

        public void Dispose() { }

        private sealed class ClerkHireDocumentEvaluationPolicy : IDocumentEvaluationPolicy {
            public DocumentEvaluationInputs Resolve(
                SignatureDifficultyRules baseDifficulty,
                SignatureRuleModifiers playerModifiers) {
                return new DocumentEvaluationInputs(baseDifficulty, SignatureRuleModifiers.None);
            }
        }

        private sealed class ClerkHireDocumentSession : IDocumentSession {
            private readonly OfficeService _office;
            private readonly OfficeService.ClerkHireDocumentClaim _claim;
            private bool _finished;

            public IDocumentEvaluationPolicy EvaluationPolicy => Policy;

            public ClerkHireDocumentSession(OfficeService office, OfficeService.ClerkHireDocumentClaim claim) {
                _office = office;
                _claim = claim;
            }

            public bool TryProcess(SignatureEvaluationResult result) {
                if (result == null) throw new ArgumentNullException(nameof(result));
                if (_finished) return false;
                bool completed = _office.TryCompletePendingClerkHire(_claim, result);
                if (completed) _finished = true;
                return completed;
            }

            public void Dispose() {
                if (_finished) return;
                _finished = true;
                _office.TryReleasePendingClerkHire(_claim);
            }
        }
    }
}
