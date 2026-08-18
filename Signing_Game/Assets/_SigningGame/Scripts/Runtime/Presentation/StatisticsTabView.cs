using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Data.Statistics;
using R3;
using Services;
using Services.Locator;
using TMPro;
using UI;
using UnityEngine;

namespace Presentation {
    /// <summary>
    /// Statistics pull tab. Rows are built once from the designer layout; values refresh
    /// from GameStatisticsService only while the tab is open.
    /// </summary>
    public sealed class StatisticsTabView : MonoBehaviour {
        [Header("Pull Tab")]
        [SerializeField] private PullTabView _pullTab;

        [Header("Configuration")]
        [SerializeField] private StatisticsTabLayoutDefinition _layout;

        [Header("Content")]
        [SerializeField] private RectTransform _contentRoot;
        [SerializeField] private StatisticRowView _rowPrefab;
        [SerializeField] private TextMeshProUGUI _categoryHeaderPrefab;

        private readonly CompositeDisposable _subscriptions = new();
        private readonly List<StatisticRowView> _rowViews = new();
        private readonly List<StatisticRowPresentationModel> _rowModels = new();
        private StatisticsTabViewModel _viewModel;
        private bool _isOpen;

        private async void Start() {
            if (_pullTab == null || _layout == null || _contentRoot == null ||
                _rowPrefab == null || _categoryHeaderPrefab == null) {
                Debug.LogError("StatisticsTabView is missing required references.", this);
                enabled = false;
                return;
            }

            ServiceLocator locator = ServiceLocator.For(this);
            try {
                await UniTask.WaitUntil(() => locator != null && locator.IsReady,
                    cancellationToken: this.GetCancellationTokenOnDestroy());
            } catch (OperationCanceledException) {
                return;
            }

            if (this == null) return;
            _viewModel = new StatisticsTabViewModel(_layout, locator.Get<GameStatisticsService>());
            BuildRows();
            _pullTab.OpenState.Subscribe(OnPullTabOpenChanged).AddTo(_subscriptions);
            _viewModel.Changed.Subscribe(_ => RefreshIfOpen()).AddTo(_subscriptions);
            _isOpen = _pullTab.IsOpen;
            RefreshIfOpen();
        }

        private void OnPullTabOpenChanged(bool isOpen) {
            _isOpen = isOpen;
            if (isOpen) RefreshIfOpen();
        }

        private void RefreshIfOpen() {
            if (!_isOpen || _viewModel == null) return;
            _viewModel.Refresh();
            for (int index = 0; index < _rowViews.Count; index++) {
                _rowViews[index].Refresh(_rowModels[index]);
            }
        }

        private void BuildRows() {
            ClearRows();
            IReadOnlyList<StatisticsCategoryPresentationModel> categories = _viewModel.Categories;
            for (int categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++) {
                StatisticsCategoryPresentationModel category = categories[categoryIndex];
                TextMeshProUGUI header = Instantiate(_categoryHeaderPrefab, _contentRoot);
                header.gameObject.SetActive(true);
                header.text = category.Title;

                IReadOnlyList<StatisticRowPresentationModel> rows = category.Rows;
                for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++) {
                    StatisticRowPresentationModel row = rows[rowIndex];
                    StatisticRowView view = Instantiate(_rowPrefab, _contentRoot);
                    view.gameObject.SetActive(true);
                    view.Bind(row);
                    _rowViews.Add(view);
                    _rowModels.Add(row);
                }
            }
        }

        private void ClearRows() {
            for (int childIndex = _contentRoot.childCount - 1; childIndex >= 0; childIndex--) {
                Transform child = _contentRoot.GetChild(childIndex);
                if (child != null) Destroy(child.gameObject);
            }

            _rowViews.Clear();
            _rowModels.Clear();
        }

        private void OnDestroy() {
            _subscriptions.Dispose();
            _viewModel?.Dispose();
        }
    }
}
