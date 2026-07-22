using TMPro;
using UnityEngine;

namespace UI {
    public class RewardIncomeDisplay : MonoBehaviour {
        private TMP_Text _text;
        
        private bool _isAccepted;
        private float _accuracy;

        private bool _isRewardSet;
        

        private void Awake() {
            _text = GetComponent<TMP_Text>();
        }


        public void Start() {
            if (!_isRewardSet) {
                Debug.LogWarning("Reward not set. Destroying...");
                Destroy(gameObject);
                return;
            }
            _text.text = $"+{(_isAccepted ? "Nice!" : "Rejected!")}$\nAcc.: {(_accuracy * 100f):0.00}%";
        }

        public void SetReward(float accuracy, bool isAccepted) {
            _isRewardSet = true;
            _isAccepted = isAccepted;
            _accuracy = accuracy;
        }
    }
}