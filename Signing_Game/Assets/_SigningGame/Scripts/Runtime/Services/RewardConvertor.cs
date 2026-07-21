using Data.Enums;
using Data.Results;
using Data.Rewards;
using R3;

namespace Services {
    public class RewardConvertor : IService {
        
        private Subject<RewardResult> _rewardSubject = new();
        
        public Observable<RewardResult> RewardObservable => _rewardSubject;

        public void CalculateReward(SignatureEvaluationResult result, RewardKind kind) {
            if (!SignatureEvaluationStatus.Accepted.Equals(result.Status)) {
                _rewardSubject.OnNext(new RewardResult(RewardStatus.RewardRejected, kind, 0, result.Similarity));
                return;
            }
            
            _rewardSubject.OnNext(new RewardResult(RewardStatus.RewardGranted, kind, 1, result.Similarity));
        }
        
        public void Dispose() {
            _rewardSubject.Dispose();
        }
    }
}