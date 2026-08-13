using System;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Enums;
using Data.Input;
using Data.Results;
using Data.Rewards;
using R3;
using Services.Locator;

namespace Services {
    public class PlayerSignatureAcceptor : IService, IInitialize {

        private ISignatureEvaluator _evaluator;
        private PlayerStatStash _stash;
        
        private Subject<DocumentHandleResult> _documentResults = new();
        
        public Observable<DocumentHandleResult> DocumentResults => _documentResults;
        
        public void Dispose() {
            _documentResults.Dispose();
        }

        public UniTask InitializeAsync(IServiceScope scope) {
            _evaluator = scope.Get<ISignatureEvaluator>();
            _stash = scope.Get<PlayerStatStash>();
            return UniTask.CompletedTask;
        }

        public bool AcceptSignature(SignatureAttempt attempt, IDocumentSession session) {
            if (attempt == null) throw new ArgumentNullException(nameof(attempt));
            if (session == null) throw new ArgumentNullException(nameof(session));

            var difficulty = new SignatureDifficultyContext(
                _stash.GetConfiguredSignatureDifficulty(),
                _stash.GetEffectiveSignatureDifficulty());
            DocumentEvaluationInputs inputs = session.EvaluationPolicy.Resolve(difficulty);
            SignatureEvaluationResult evaluationResult = _evaluator.Evaluate(
                attempt,
                _stash.GetActivePreset(),
                inputs.Difficulty,
                inputs.Modifiers);
            if (!session.TryProcess(evaluationResult)) return false;

            _documentResults.OnNext(new DocumentHandleResult(
                session.Kind,
                evaluationResult.Status == SignatureEvaluationStatus.Accepted
                    ? RewardStatus.RewardGranted
                    : RewardStatus.RewardRejected,
                evaluationResult.Similarity));
            return true;
        }
    }
}
