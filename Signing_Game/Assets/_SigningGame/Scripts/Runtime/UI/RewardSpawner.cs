using Data.Enums;
using Data.Rewards;
using R3;
using Services;
using Services.Locator;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UI {
    public class RewardSpawner : MonoBehaviour {
        
        [SerializeField] private RewardIncomeDisplay _rewardPrefab;
        [SerializeField] private Canvas _canvas;
        
        private RewardConvertor _rewardConvertor;

        public void Start() {
            ServiceLocator.For(this).Get(out _rewardConvertor);
            _rewardConvertor.RewardObservable.Where(reward => RewardStatus.RewardGranted.Equals(reward.Status)).Subscribe(RewardGranted);
        }

        private void RewardGranted(RewardResult reward) {
            var newReward = Instantiate(_rewardPrefab, _canvas.transform);
            newReward.SetReward(reward.Amount, reward.Accuracy);
            newReward.transform.position = Mouse.current.position.ReadValue();
        }
    }
}