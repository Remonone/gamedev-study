using System;
using System.Collections.Generic;
using R3;
using Services;
using Services.Locator;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.Pool;
using Utils;

namespace Presentation {
    public class WalletView : MonoBehaviour {
        [SerializeField] private TextMeshProUGUI _balanceText;
        [Header("Income notifications")]
        [SerializeField] private RewardIncomeDisplay _incomePrefab;
        [SerializeField] private Canvas _incomeCanvas;
        [SerializeField] private RectTransform _incomeSpawnRoot;
        [SerializeField] private Vector2 _incomeOffset = new(0f, 24f);
        [SerializeField, Min(0)] private int _incomePoolCapacity = 8;
        [SerializeField, Min(1)] private int _incomePoolMaxSize = 32;

        private readonly CompositeDisposable _subscriptions = new();
        private readonly List<RewardIncomeDisplay> _activeIncomeDisplays = new();
        private readonly IncomeNotificationQueue _incomeQueue = new();
        private WalletViewModel _viewModel;
        private ObjectPool<RewardIncomeDisplay> _incomePool;
        private bool _incomeInitialized;

        private void Start() {
            var walletService = ServiceLocator.For(this).Get<WalletService>();
            _viewModel = new WalletViewModel(walletService);
            _viewModel.Balance.Subscribe(OnBalanceChanged).AddTo(_subscriptions);
            if (!TryInitializeIncome()) return;
            _viewModel.Credited.Subscribe(QueueIncome).AddTo(_subscriptions);
        }

        private void Update() {
            if (!_incomeInitialized || !_incomeQueue.TryDequeueDue(Time.unscaledTimeAsDouble, out Value income)) {
                return;
            }

            RewardIncomeDisplay display = _incomePool.Get();
            display.RectTransform.anchoredPosition = GetIncomeSpawnPosition();
            display.ShowIncome($"+{income}");
        }

        private void OnBalanceChanged(string balance) {
            _balanceText.text = $"{balance}$";
        }

        private bool TryInitializeIncome() {
            if (_incomeInitialized) return true;
            if (_balanceText == null || _incomePrefab == null || _incomeCanvas == null || _incomeSpawnRoot == null) {
                Debug.LogError("WalletView requires balance text, income prefab, canvas, and spawn root references.", this);
                return false;
            }

            Transform canvasTransform = _incomeCanvas.transform;
            if (_incomeSpawnRoot != canvasTransform && !_incomeSpawnRoot.IsChildOf(canvasTransform)) {
                Debug.LogError("WalletView income spawn root must be under the configured canvas.", this);
                return false;
            }

            if (_incomeSpawnRoot == transform || _incomeSpawnRoot.IsChildOf(transform)) {
                Debug.LogError("WalletView income spawn root must stay outside the wallet layout hierarchy.", this);
                return false;
            }

            _incomePool = new ObjectPool<RewardIncomeDisplay>(
                CreateIncomeDisplay,
                OnIncomeTaken,
                OnIncomeReturned,
                OnIncomeDestroyed,
                true,
                _incomePoolCapacity,
                _incomePoolMaxSize);
            _incomeInitialized = true;
            return true;
        }

        private void QueueIncome(Value income) {
            if (!isActiveAndEnabled) return;
            _incomeQueue.Enqueue(income, Time.unscaledTimeAsDouble);
        }

        private RewardIncomeDisplay CreateIncomeDisplay() {
            RewardIncomeDisplay display = Instantiate(_incomePrefab, _incomeSpawnRoot);
            display.SetReleaseCallback(ReleaseIncomeDisplay);
            display.ResetForPool();
            return display;
        }

        private void OnIncomeTaken(RewardIncomeDisplay display) {
            if (display == null) return;
            display.transform.SetParent(_incomeSpawnRoot, false);
            display.SetReleaseCallback(ReleaseIncomeDisplay);
            display.gameObject.SetActive(true);
            _activeIncomeDisplays.Add(display);
        }

        private void OnIncomeReturned(RewardIncomeDisplay display) {
            if (display == null) return;
            _activeIncomeDisplays.Remove(display);
            display.ResetForPool();
        }

        private void OnIncomeDestroyed(RewardIncomeDisplay display) {
            if (display == null) return;
            _activeIncomeDisplays.Remove(display);
            display.SetReleaseCallback(null);
            Destroy(display.gameObject);
        }

        private void ReleaseIncomeDisplay(RewardIncomeDisplay display) {
            if (_incomePool == null || display == null) return;
            _incomePool.Release(display);
        }

        private Vector2 GetIncomeSpawnPosition() {
            Vector3 balanceCenter = _balanceText.rectTransform.TransformPoint(_balanceText.rectTransform.rect.center);
            Vector3 localPosition = _incomeSpawnRoot.InverseTransformPoint(balanceCenter);
            return new Vector2(localPosition.x, localPosition.y) + _incomeOffset;
        }

        private void OnDestroy() {
            _subscriptions.Dispose();
            _incomeQueue.Clear();
            for (int index = _activeIncomeDisplays.Count - 1; index >= 0; index--) {
                RewardIncomeDisplay display = _activeIncomeDisplays[index];
                if (display == null) continue;
                display.SetReleaseCallback(null);
                Destroy(display.gameObject);
            }

            _activeIncomeDisplays.Clear();
            _incomePool?.Clear();
            _incomePool = null;
            _incomeInitialized = false;
            _viewModel?.Dispose();
            _viewModel = null;
        }
    }
}
