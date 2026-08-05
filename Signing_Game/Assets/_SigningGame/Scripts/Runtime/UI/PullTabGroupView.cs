using System;
using R3;
using UnityEngine;

namespace UI {
    public sealed class PullTabGroupView : MonoBehaviour {
        [SerializeField] private PullTabView[] _tabs = Array.Empty<PullTabView>();

        private CompositeDisposable _subscriptions;
        private bool _isClosingPeers;

        private void OnEnable() {
            _subscriptions = new CompositeDisposable();
            for (int index = 0; index < _tabs.Length; index++) {
                PullTabView tab = _tabs[index];
                if (tab == null) continue;
                int capturedIndex = index;
                tab.OpenState
                    .Where(isOpen => isOpen)
                    .Subscribe(_ => ClosePeers(capturedIndex))
                    .AddTo(_subscriptions);
            }
        }

        private void OnDisable() {
            _subscriptions?.Dispose();
            _subscriptions = null;
            _isClosingPeers = false;
        }

        private void OnDestroy() {
            _subscriptions?.Dispose();
            _subscriptions = null;
        }

        private void ClosePeers(int openIndex) {
            if (_isClosingPeers) return;
            _isClosingPeers = true;
            try {
                for (int index = 0; index < _tabs.Length; index++) {
                    if (index == openIndex || _tabs[index] == null || !_tabs[index].IsOpen) continue;
                    _tabs[index].SetOpen(false);
                }
            } finally {
                _isClosingPeers = false;
            }
        }
    }
}
