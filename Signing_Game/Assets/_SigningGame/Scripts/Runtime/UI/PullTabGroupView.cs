using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

namespace UI {
    public sealed class PullTabGroupView : MonoBehaviour {
        [SerializeField] private PullTabView[] _tabs = Array.Empty<PullTabView>();

        private readonly List<PullTabView> _runtimeTabs = new();
        private CompositeDisposable _subscriptions;
        private bool _isClosingPeers;

        public void RegisterTab(PullTabView tab) {
            if (tab == null || _runtimeTabs.Contains(tab)) return;

            _runtimeTabs.Add(tab);
            if (isActiveAndEnabled) Subscribe(tab);
        }

        public void UnregisterTab(PullTabView tab) => _runtimeTabs.Remove(tab);

        private void OnEnable() {
            _subscriptions = new CompositeDisposable();
            for (int index = 0; index < _tabs.Length; index++) {
                PullTabView tab = _tabs[index];
                if (tab == null) continue;
                Subscribe(tab);
            }

            for (int index = 0; index < _runtimeTabs.Count; index++) Subscribe(_runtimeTabs[index]);
        }

        private void OnDisable() {
            _subscriptions?.Dispose();
            _subscriptions = null;
            _isClosingPeers = false;
        }

        private void OnDestroy() {
            _subscriptions?.Dispose();
            _subscriptions = null;
            _runtimeTabs.Clear();
        }

        private void Subscribe(PullTabView tab) {
            tab.OpenState
                .Where(isOpen => isOpen)
                .Subscribe(_ => ClosePeers(tab))
                .AddTo(_subscriptions);
        }

        private void ClosePeers(PullTabView openTab) {
            if (_isClosingPeers) return;
            _isClosingPeers = true;
            try {
                for (int index = 0; index < _tabs.Length; index++) {
                    PullTabView tab = _tabs[index];
                    if (tab == null || tab == openTab || !tab.IsOpen) continue;
                    tab.SetOpen(false);
                }

                for (int index = 0; index < _runtimeTabs.Count; index++) {
                    PullTabView tab = _runtimeTabs[index];
                    if (tab == null || tab == openTab || !tab.IsOpen) continue;
                    tab.SetOpen(false);
                }
            } finally {
                _isClosingPeers = false;
            }
        }
    }
}
