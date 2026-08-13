using System;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Documents;
using Data.Results;
using Data.Rules;
using Services.Locator;
using R3;

namespace Services {
    public sealed class UpgradeDocumentProducer : IService, IInitialize, IDocumentProducer {
        private static readonly IDocumentEvaluationPolicy Policy = new UpgradeDocumentEvaluationPolicy();
        private UpgradeService _upgrades;

        public int Priority => 100;
        public Observable<Unit> OffersChanged => _upgrades.DocumentOffersChanged;

        public UniTask InitializeAsync(IServiceScope scope) {
            _upgrades = scope.Get<UpgradeService>();
            return UniTask.CompletedTask;
        }

        public bool TryProduce(out IDocumentSession session) {
            session = null;
            return TryPeekOffer(out DocumentOffer offer) && TryProduce(offer.Key, out session);
        }

        public bool TryPeekOffer(out DocumentOffer offer) {
            return _upgrades.TryPeekPendingUpgradeDocument(out offer);
        }

        public bool TryProduce(DocumentOfferKey offerKey, out IDocumentSession session) {
            session = null;
            if (offerKey.Kind != DocumentKind.Upgrade ||
                !_upgrades.TryClaimPendingUpgrade(offerKey.DomainId, out UpgradeService.UpgradeDocumentClaim claim)) {
                return false;
            }

            session = new UpgradeDocumentSession(_upgrades, claim);
            return true;
        }

        public void Dispose() { }

        private sealed class UpgradeDocumentEvaluationPolicy : IDocumentEvaluationPolicy {
            public DocumentEvaluationInputs Resolve(SignatureDifficultyContext difficulty) {
                return new DocumentEvaluationInputs(difficulty.ConfiguredDifficulty, SignatureRuleModifiers.None);
            }
        }

        private sealed class UpgradeDocumentSession : IDocumentSession {
            private readonly UpgradeService _upgrades;
            private readonly UpgradeService.UpgradeDocumentClaim _claim;
            private bool _finished;

            public DocumentKind Kind => DocumentKind.Upgrade;
            public IDocumentEvaluationPolicy EvaluationPolicy => Policy;

            public UpgradeDocumentSession(
                UpgradeService upgrades,
                UpgradeService.UpgradeDocumentClaim claim) {
                _upgrades = upgrades;
                _claim = claim;
            }

            public bool TryProcess(SignatureEvaluationResult result) {
                if (result == null) throw new ArgumentNullException(nameof(result));
                if (_finished) return false;
                bool completed = _upgrades.TryCompletePendingUpgrade(_claim, result);
                if (completed) _finished = true;
                return completed;
            }

            public void Dispose() {
                if (_finished) return;
                _finished = true;
                _upgrades.TryReleasePendingUpgrade(_claim);
            }
        }
    }
}
