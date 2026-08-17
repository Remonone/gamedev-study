using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Data.Tutorial;
using DG.Tweening;
using R3;
using Services;
using UI;
using UnityEngine;

namespace Presentation.Tutorial {
    /// <summary>
    /// Scene bridge between pull-tab views and the tutorial service: publishes tab-opened
    /// interactions and pulses a tab handle while a slide awaits that tab's opening.
    /// </summary>
    public sealed class TutorialTabBridge : MonoBehaviour {
        [Serializable]
        private struct TabBinding {
            public string TabId;
            public PullTabView Tab;
        }

        [SerializeField] private TabBinding[] _tabs = Array.Empty<TabBinding>();
        [SerializeField, Min(0.05f)] private float _pulseScale = 1.15f;
        [SerializeField, Min(0.1f)] private float _pulseDuration = 0.5f;

        private readonly Dictionary<string, Tween> _pulses = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector3> _originalScales = new();
        private CompositeDisposable _subscriptions;

        private void Start() {
            TutorialBridgeUtility.BindWhenReady(this, Subscribe);
        }

        private void OnDisable() {
            StopPulse();
        }

        private void OnDestroy() {
            _subscriptions?.Dispose();
            _subscriptions = null;
            StopPulse();
        }

        public void Pulse(string tabId) {
            if (string.IsNullOrEmpty(tabId)) return;

            TabBinding? binding = FindBinding(tabId);
            if (binding == null || binding.Value.Tab == null) return;
            RectTransform handle = (RectTransform)binding.Value.Tab.transform;
            if (handle == null || _pulses.ContainsKey(tabId)) return;

            _originalScales[tabId] = handle.localScale;
            _pulses[tabId] = handle
                .DOScale(handle.localScale * _pulseScale, _pulseDuration)
                .SetEase(Ease.InOutQuad)
                .SetLoops(-1, LoopType.Yoyo)
                .SetTarget(this);
        }

        public void StopPulse() {
            foreach (KeyValuePair<string, Tween> pair in _pulses) {
                pair.Value?.Kill();
                if (_originalScales.TryGetValue(pair.Key, out Vector3 original) &&
                    FindBinding(pair.Key) is { } binding && binding.Tab != null) {
                    ((RectTransform)binding.Tab.transform).localScale = original;
                }
            }

            _pulses.Clear();
            _originalScales.Clear();
        }

        private void Subscribe(TutorialService tutorial) {
            _subscriptions = new CompositeDisposable();
            for (int index = 0; index < _tabs.Length; index++) {
                PullTabView tab = _tabs[index].Tab;
                if (tab == null) continue;

                string tabId = _tabs[index].TabId;
                tab.OpenState
                    .Where(isOpen => isOpen)
                    .Subscribe(_ =>
                        tutorial.NotifyInteraction(new TutorialInteractionEvent(TutorialInteractionKind.TabOpened, tabId)))
                    .AddTo(_subscriptions);
            }
        }

        private TabBinding? FindBinding(string tabId) {
            for (int index = 0; index < _tabs.Length; index++) {
                if (string.Equals(_tabs[index].TabId, tabId, StringComparison.Ordinal)) return _tabs[index];
            }

            return null;
        }
    }
}
