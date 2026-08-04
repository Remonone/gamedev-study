using System;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Data.Enums;
using Data.Results;
using Data.Rules;
using Services.Locator;
using Utils;

namespace Services {
    public sealed class NormalDocumentProducer : IService, IInitialize, IDocumentProducer {
        private static readonly IDocumentEvaluationPolicy Policy = new PlayerDocumentEvaluationPolicy();

        private DocumentGeneratorService _generator;
        private IMoneyAggregator _aggregator;
        private IReadOnlyCacheData<IncomeEntries> _incomeData;

        public int Priority => 0;

        public UniTask InitializeAsync(IServiceScope scope) {
            _generator = scope.Get<DocumentGeneratorService>();
            _aggregator = scope.Get<IMoneyAggregator>();
            _incomeData = scope.Get<PlayerStatStash>().IncomeData;
            return UniTask.CompletedTask;
        }

        public bool TryProduce(out IDocumentSession session) {
            session = null;
            if (!_generator.TryReserveDocument(out DocumentGeneratorService.DocumentReservation reservation)) {
                return false;
            }

            session = new NormalDocumentSession(_generator, reservation, _aggregator, _incomeData);
            return true;
        }

        public void Dispose() { }

        private sealed class PlayerDocumentEvaluationPolicy : IDocumentEvaluationPolicy {
            public DocumentEvaluationInputs Resolve(
                SignatureDifficultyRules baseDifficulty,
                SignatureRuleModifiers playerModifiers) {
                return new DocumentEvaluationInputs(baseDifficulty, playerModifiers);
            }
        }

        private sealed class NormalDocumentSession : IDocumentSession {
            private readonly DocumentGeneratorService _generator;
            private readonly DocumentGeneratorService.DocumentReservation _reservation;
            private readonly IMoneyAggregator _aggregator;
            private readonly IReadOnlyCacheData<IncomeEntries> _incomeData;
            private bool _finished;

            public IDocumentEvaluationPolicy EvaluationPolicy => Policy;

            public NormalDocumentSession(
                DocumentGeneratorService generator,
                DocumentGeneratorService.DocumentReservation reservation,
                IMoneyAggregator aggregator,
                IReadOnlyCacheData<IncomeEntries> incomeData) {
                _generator = generator;
                _reservation = reservation;
                _aggregator = aggregator;
                _incomeData = incomeData;
            }

            public bool TryProcess(SignatureEvaluationResult result) {
                if (result == null) throw new ArgumentNullException(nameof(result));
                if (_finished || !_generator.TryCommitReservation(_reservation)) return false;

                _finished = true;
                if (result.Status == SignatureEvaluationStatus.Accepted) SendReward(result);
                return true;
            }

            public void Dispose() {
                if (_finished) return;
                _finished = true;
                _generator.TryCancelReservation(_reservation);
            }

            private void SendReward(SignatureEvaluationResult result) {
                IncomeEntries income = _incomeData.Value;
                double accuracyBonus = Math.Min(
                    Math.Min(result.Similarity / income.MinMultiplyScale, 1d),
                    income.MaxMultiplicationScale);
                Value reward = income.IncomePerDocument * accuracyBonus;
                _aggregator.AddMoney(reward);
            }
        }
    }
}
