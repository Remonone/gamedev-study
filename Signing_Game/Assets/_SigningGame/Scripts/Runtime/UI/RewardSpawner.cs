using System;
using System.Collections.Generic;
using Data.Documents;
using Data.Enums;
using Data.Rewards;
using R3;
using Services;
using Services.Locator;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Pool;

namespace UI {
    public class RewardSpawner : MonoBehaviour {
        [SerializeField] private RewardIncomeDisplay _rewardPrefab;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _spawnRoot;
        [SerializeField] private Vector2 _screenOffset = new(0f, 24f);
        [SerializeField, Min(0)] private int _defaultPoolCapacity = 8;
        [SerializeField, Min(1)] private int _maxPoolSize = 32;

        private readonly List<RewardIncomeDisplay> _activeRewards = new();
        private PlayerSignatureAcceptor _rewardConvertor;
        private ObjectPool<RewardIncomeDisplay> _pool;
        private IDisposable _resultsSubscription;

        private void Awake() {
            EnsurePool();
        }

        private void Start() {
            ServiceLocator.For(this).Get(out _rewardConvertor);
            _resultsSubscription = _rewardConvertor.DocumentResults.Subscribe(OnDocumentHandled);
        }

        private void OnDestroy() {
            _resultsSubscription?.Dispose();
            _resultsSubscription = null;

            for (int index = _activeRewards.Count - 1; index >= 0; index--) {
                RewardIncomeDisplay reward = _activeRewards[index];
                if (reward == null) continue;
                reward.SetReleaseCallback(null);
                Destroy(reward.gameObject);
            }

            _activeRewards.Clear();
            _pool?.Clear();
            _pool = null;
        }

        private void OnDocumentHandled(DocumentHandleResult result) {
            if (!isActiveAndEnabled || _rewardPrefab == null) return;

            EnsurePool();
            RewardIncomeDisplay reward = _pool.Get();
            reward.RectTransform.anchoredPosition = GetSpawnPosition();
            reward.Show(
                result.Accuracy,
                result.Status == RewardStatus.RewardGranted,
                GetAcceptedText(result.Kind));
        }

        private void EnsurePool() {
            if (_pool != null) return;

            _pool = new ObjectPool<RewardIncomeDisplay>(
                CreateReward,
                OnRewardTaken,
                OnRewardReturned,
                OnRewardDestroyed,
                true,
                _defaultPoolCapacity,
                _maxPoolSize);
        }

        private RewardIncomeDisplay CreateReward() {
            Transform parent = GetSpawnParent();
            RewardIncomeDisplay reward = Instantiate(_rewardPrefab, parent);
            reward.SetReleaseCallback(ReleaseReward);
            reward.ResetForPool();
            return reward;
        }

        private void OnRewardTaken(RewardIncomeDisplay reward) {
            if (reward == null) return;

            reward.transform.SetParent(GetSpawnParent(), false);
            reward.SetReleaseCallback(ReleaseReward);
            reward.gameObject.SetActive(true);
            _activeRewards.Add(reward);
        }

        private void OnRewardReturned(RewardIncomeDisplay reward) {
            if (reward == null) return;

            _activeRewards.Remove(reward);
            reward.ResetForPool();
        }

        private void OnRewardDestroyed(RewardIncomeDisplay reward) {
            if (reward == null) return;

            _activeRewards.Remove(reward);
            reward.SetReleaseCallback(null);
            Destroy(reward.gameObject);
        }

        private void ReleaseReward(RewardIncomeDisplay reward) {
            if (_pool == null || reward == null) return;

            _pool.Release(reward);
        }

        private Vector2 GetSpawnPosition() {
            RectTransform parent = GetSpawnParentRect();
            if (parent == null) return _screenOffset;

            Vector2 screenPosition = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Camera eventCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent,
                screenPosition,
                eventCamera,
                out Vector2 localPosition)
                ? localPosition + _screenOffset
                : _screenOffset;
        }

        private Transform GetSpawnParent() {
            return GetSpawnParentRect() ?? transform;
        }

        private RectTransform GetSpawnParentRect() {
            if (_spawnRoot != null) return _spawnRoot;

            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            if (_canvas != null && _canvas.transform is RectTransform canvasRect) return canvasRect;

            return transform as RectTransform;
        }

        private static string GetAcceptedText(DocumentKind kind) {
            return kind switch {
                DocumentKind.Upgrade => "Upgraded",
                DocumentKind.ClerkHire => "Hired",
                DocumentKind.Bill => "Initiated",
                DocumentKind.Practice => "Applied",
                _ => "Approved"
            };
        }
    }
}
