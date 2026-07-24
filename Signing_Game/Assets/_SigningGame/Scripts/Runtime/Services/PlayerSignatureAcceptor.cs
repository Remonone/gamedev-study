using System;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Enums;
using Data.Input;
using Data.Results;
using Data.Rewards;
using R3;
using Services.Locator;
using Utils;

namespace Services {
    public class PlayerSignatureAcceptor : IService, IInitialize {
        
        private ISignatureEvaluator _evaluator;
        private PlayerStatStash _stash;
        private IMoneyAggregator _aggregator;
        private DifficultyProfileEvaluator _difficultyEvaluator;
        private IReadOnlyCacheData<IncomeEntries> _incomeData;
        
        private Subject<DocumentHandleResult> _documentResults = new();
        
        public Observable<DocumentHandleResult> DocumentResults => _documentResults;
        
        public void Dispose() {
            _documentResults.Dispose();
        }

        public UniTask InitializeAsync(IServiceScope scope) {
            _evaluator = scope.Get<ISignatureEvaluator>();
            _difficultyEvaluator = scope.Get<DifficultyProfileEvaluator>();
            _aggregator = scope.Get<IMoneyAggregator>();
            _stash = scope.Get<PlayerStatStash>();
            _incomeData = _stash.IncomeData;
            return UniTask.CompletedTask;
        }

        public void AcceptSignature(SignatureAttempt attempt) {
            var difficulty = _difficultyEvaluator.GetDifficultyProfile();
            var modifiers = _stash.GetSignatureModifiers();
            var preset = _stash.GetActivePreset();
            var evaluationResult = _evaluator.Evaluate(attempt, preset, difficulty, modifiers);
            if (evaluationResult.Status == SignatureEvaluationStatus.Accepted) SendReward(evaluationResult);
            _documentResults.OnNext(new DocumentHandleResult(evaluationResult.Status == SignatureEvaluationStatus.Accepted ? RewardStatus.RewardGranted : RewardStatus.RewardRejected, 
                evaluationResult.Similarity));
        }

        private void SendReward(SignatureEvaluationResult evaluationResult) {
            var income = _incomeData.Value.IncomePerDocument;
            var maxMultiplicationScale = _incomeData.Value.MaxMultiplicationScale;
            var minMultiplyScale = _incomeData.Value.MinMultiplyScale;
            
            var accuracyBonus = Math.Min(Math.Min(evaluationResult.Similarity / minMultiplyScale, 1), maxMultiplicationScale);
            var result = income * accuracyBonus;
            _aggregator.AddMoney(result);
        }
    }
}