using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using Utils;

namespace UI {
    public class RewardIncomeDisplay : MonoBehaviour {
        private TMP_Text _text;
        
        private Value _reward;

        private bool _isRewardSet;
        private float _accuracy;

        private void Awake() {
            _text = GetComponent<TMP_Text>();
        }


        public void Start() {
            if (!_isRewardSet) {
                Debug.LogWarning("Reward not set. Destroying...");
                Destroy(gameObject);
                return;
            }
            _text.text = $"+{_reward}$\nAcc.: {(_accuracy * 100f):0.00}%";
        }

        public void SetReward(Value reward, float accuracy) {
            _isRewardSet = true;
            _reward = reward;
            _accuracy = accuracy;
        }
    }
}