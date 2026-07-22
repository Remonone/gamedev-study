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
        
        private PlayerSignatureAcceptor _rewardConvertor;

        public void Start() {
            ServiceLocator.For(this).Get(out _rewardConvertor);
            _rewardConvertor.DocumentResults.Subscribe(OnDocumentHandled);
        }

        private void OnDocumentHandled(DocumentHandleResult result) {
            var newReward = Instantiate(_rewardPrefab, _canvas.transform);
            newReward.SetReward(result.Accuracy, result.Status == RewardStatus.RewardGranted ? true : false);
            newReward.transform.position = Mouse.current.position.ReadValue();
        }
    }
}