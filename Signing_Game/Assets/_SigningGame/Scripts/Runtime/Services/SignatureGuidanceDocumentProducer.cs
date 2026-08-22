using System;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Documents;
using Data.Results;
using Data.Rules;
using R3;
using Services.Locator;

namespace Services {
    public sealed class SignatureGuidanceDocumentProducer : IService, IInitialize, IDocumentProducer {
        private const string OfferId = "signature-guidance";
        private static readonly IDocumentEvaluationPolicy Policy = new GuidanceEvaluationPolicy();

        private readonly Subject<Unit> _offersChanged = new();
        private bool _requested;

        public int Priority => 400;
        public Observable<Unit> OffersChanged => _offersChanged;

        public UniTask InitializeAsync(IServiceScope scope) {
            return UniTask.CompletedTask;
        }

        public bool Request() {
            if (_requested) return false;

            _requested = true;
            _offersChanged.OnNext(Unit.Default);
            return true;
        }

        public bool TryPeekOffer(out DocumentOffer offer) {
            if (!_requested) {
                offer = null;
                return false;
            }

            offer = new DocumentOffer(
                new DocumentOfferKey(DocumentKind.SignatureGuidance, OfferId),
                true,
                header: "SIGNATURE GUIDANCE");
            return true;
        }

        public bool TryProduce(DocumentOfferKey offerKey, out IDocumentSession session) {
            session = null;
            if (!_requested || offerKey.Kind != DocumentKind.SignatureGuidance ||
                !string.Equals(offerKey.DomainId, OfferId, StringComparison.Ordinal)) {
                return false;
            }

            _requested = false;
            _offersChanged.OnNext(Unit.Default);
            session = new SignatureGuidanceDocumentSession();
            return true;
        }

        public void Dispose() {
            _offersChanged.Dispose();
            _requested = false;
        }

        private sealed class GuidanceEvaluationPolicy : IDocumentEvaluationPolicy {
            public DocumentEvaluationInputs Resolve(SignatureDifficultyContext difficulty) {
                return new DocumentEvaluationInputs(difficulty.ConfiguredDifficulty, SignatureRuleModifiers.None);
            }
        }

        private sealed class SignatureGuidanceDocumentSession : IDocumentSession {
            private bool _finished;

            public DocumentKind Kind => DocumentKind.SignatureGuidance;
            public IDocumentEvaluationPolicy EvaluationPolicy => Policy;

            public bool TryProcess(SignatureEvaluationResult result, bool isStamped = false) {
                if (result == null) throw new ArgumentNullException(nameof(result));
                if (_finished) return false;

                _finished = true;
                return true;
            }

            public void Dispose() {
                _finished = true;
            }
        }
    }
}
