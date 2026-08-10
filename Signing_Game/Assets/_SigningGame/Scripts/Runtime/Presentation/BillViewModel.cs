using System;
using System.Collections.Generic;
using System.Globalization;
using Data.Bills;
using R3;
using Services;
using UnityEngine;

namespace Presentation {
    public sealed class BillViewModel : IDisposable {
        private readonly BillService _bills;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly ReactiveProperty<BillTab> _selectedTab = new(BillTab.Catalog);
        private readonly ReactiveProperty<bool> _isAvailable = new(false);
        private readonly Subject<Unit> _changed = new();
        private readonly HashSet<long> _expandedCompletions = new();
        private readonly List<BillCardPresentationModel> _catalog = new();
        private readonly List<BillCardPresentationModel> _active = new();
        private readonly List<BillCardPresentationModel> _completed = new();

        public ReadOnlyReactiveProperty<BillTab> SelectedTab => _selectedTab;
        public Observable<Unit> Changed => _changed;
        public IReadOnlyList<BillCardPresentationModel> Catalog => _catalog;
        public IReadOnlyList<BillCardPresentationModel> Active => _active;
        public IReadOnlyList<BillCardPresentationModel> Completed => _completed;
        public bool HasPendingSignature => _bills.Pending != null;
        public int ActiveLimit => _bills.ActiveProjectLimit;
        
        public Observable<bool> Availability => _isAvailable;

        public BillViewModel(BillService bills) {
            _bills = bills ?? throw new ArgumentNullException(nameof(bills));
            _bills.Changed.Subscribe(_ => Rebuild()).AddTo(_subscriptions);
            Rebuild();
        }

        public void SelectTab(BillTab tab) {
            if (!Enum.IsDefined(typeof(BillTab), tab) || _selectedTab.Value == tab) return;
            _selectedTab.Value = tab;
            Rebuild();
        }

        public bool Purchase(long optionId) => _bills.TryPurchase(optionId);

        public bool SetPriority(long instanceId, int priority) {
            return _bills.TrySetPriorityWeight(instanceId, priority);
        }

        public void ToggleCompletion(long completionOrder) {
            if (!_expandedCompletions.Add(completionOrder)) _expandedCompletions.Remove(completionOrder);
            Rebuild();
        }

        public string GetEmptyMessage(BillTab tab) {
            return tab switch {
                BillTab.Catalog when _bills.Pending != null => "A purchased bill is awaiting your signature.",
                BillTab.Catalog when _bills.ActiveBills.Count >= _bills.ActiveProjectLimit =>
                    "All active bill slots are occupied.",
                BillTab.Catalog => "No bill definitions are currently available.",
                BillTab.Active => "No active bills.",
                BillTab.Completed => "No completed bills.",
                _ => string.Empty
            };
        }

        public void Dispose() {
            _subscriptions.Dispose();
            _selectedTab.Dispose();
            _isAvailable.Dispose();
            _changed.Dispose();
            _catalog.Clear();
            _active.Clear();
            _completed.Clear();
            _expandedCompletions.Clear();
        }

        private void Rebuild() {
            var availability = _bills.IsUnlocked;
            if (_isAvailable.Value != availability) _isAvailable.Value = availability; 
            _catalog.Clear();
            _active.Clear();
            _completed.Clear();
            BuildCatalog();
            BuildActive();
            BuildCompleted();
            _changed.OnNext(Unit.Default);
        }

        private void BuildCatalog() {
            IReadOnlyList<GeneratedBillOption> options = _bills.Catalog;
            for (int index = 0; index < options.Count; index++) {
                GeneratedBillOption option = options[index];
                IReadOnlyList<BillPurchaseBlocker> blockers = _bills.GetPurchaseBlockers(option.OptionId);
                _catalog.Add(new BillCardPresentationModel(
                    BillCardKind.Catalog,
                    option.OptionId,
                    option.Reward.Name,
                    BillDescriptionFormatter.FormatCatalog(_bills, option),
                    option.Reward.Icon,
                    BuildRequirements(option),
                    _bills.ResolvePrice(option),
                    blockers.Count == 0,
                    FormatBlockers(blockers)));
            }
        }

        private void BuildActive() {
            IReadOnlyList<ActiveBillState> activeBills = _bills.ActiveBills;
            for (int index = 0; index < activeBills.Count; index++) {
                ActiveBillState active = activeBills[index];
                double target = Math.Max(double.Epsilon, active.Option.RequiredProgress);
                float progress = (float)Math.Clamp(active.Progress / target, 0d, 1d);
                _active.Add(new BillCardPresentationModel(
                    BillCardKind.Active,
                    active.InstanceId,
                    active.Option.Reward.Name,
                    BillDescriptionFormatter.FormatActive(_bills, active),
                    active.Option.Reward.Icon,
                    progress: progress,
                    progressText: $"{active.Progress.ToString("0.##", CultureInfo.InvariantCulture)} / {target.ToString("0.##", CultureInfo.InvariantCulture)}",
                    priority: active.Weight,
                    maximumPriority: _bills.MaximumPriorityWeight));
            }
        }

        private void BuildCompleted() {
            IReadOnlyList<BillCompletionRecord> completions = _bills.PrepareCompletionStatisticsSnapshot();
            for (int index = completions.Count - 1; index >= 0; index--) {
                BillCompletionRecord completion = completions[index];
                bool expanded = _expandedCompletions.Contains(completion.CompletionOrder);
                _completed.Add(new BillCardPresentationModel(
                    BillCardKind.Completed,
                    completion.CompletionOrder,
                    completion.Reward.Name,
                    BillDescriptionFormatter.FormatCompleted(_bills, completion),
                    completion.Reward.Icon,
                    isExpanded: expanded,
                    statistics: expanded ? BuildStatistics(completion) : null));
            }
        }

        private IReadOnlyList<BillRequirementPresentationModel> BuildRequirements(GeneratedBillOption option) {
            var result = new List<BillRequirementPresentationModel>(option.Requirements.Count);
            for (int index = 0; index < option.Requirements.Count; index++) {
                BillRequirementSnapshot requirement = option.Requirements[index];
                bool satisfied = _bills.IsRequirementSatisfied(option, requirement);
                _bills.TryGetRequirementPresentation(requirement.TemplateId, out BillRequirementPresentationInfo info);
                string name = string.IsNullOrWhiteSpace(info.DisplayName)
                    ? GetRequirementName(requirement.Kind)
                    : info.DisplayName;
                string value = requirement.Kind == BillRequirementKind.OwnedUpgrade
                    ? "1"
                    : requirement.NumericTarget.ToString(CultureInfo.InvariantCulture);
                string details = string.IsNullOrWhiteSpace(info.ShortDescription)
                    ? GetRequirementDescription(requirement)
                    : info.ShortDescription;
                Color color = info.Color.a <= 0f ? GetRequirementColor(requirement.Kind) : info.Color;
                string tooltip = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>■</color> {details}";
                result.Add(new BillRequirementPresentationModel($"{name} {value}", tooltip, color, satisfied));
            }
            return result;
        }

        private IReadOnlyList<BillStatisticPresentationModel> BuildStatistics(BillCompletionRecord completion) {
            var result = new List<BillStatisticPresentationModel>();
            if (completion.HasCompleteWorkStatistics && completion.Option != null) {
                result.Add(new BillStatisticPresentationModel(
                    "Documents processed",
                    completion.ProcessedDocumentCount.ToString(CultureInfo.InvariantCulture)));
                result.Add(new BillStatisticPresentationModel(
                    "Required work",
                    completion.Option.RequiredProgress.ToString("0.##", CultureInfo.InvariantCulture)));
                result.Add(new BillStatisticPresentationModel(
                    "Work time",
                    FormatDuration(completion.ElapsedWorkSeconds)));
                result.Add(new BillStatisticPresentationModel("Purchase cost", $"{completion.PaidCost}$"));
            }
            else result.Add(new BillStatisticPresentationModel("Work history", "Unavailable (legacy save)"));

            result.Add(new BillStatisticPresentationModel(
                "Completion payout",
                completion.HasCompletionPayout
                    ? $"{completion.ActualCompletionPayout}$"
                    : "Unavailable (legacy save)"));
            if (completion.AdditionalGeneratedDocuments > 0d) {
                result.Add(new BillStatisticPresentationModel(
                    "Attributed extra documents",
                    completion.AdditionalGeneratedDocuments.ToString("0.##", CultureInfo.InvariantCulture)));
            }
            if (!completion.AdditionalIncome.IsZero) {
                result.Add(new BillStatisticPresentationModel(
                    "Attributed extra income",
                    $"{completion.AdditionalIncome}$"));
            }
            result.AddRange(BillDescriptionFormatter.BuildModifierRows(_bills, completion));
            return result;
        }

        private string FormatBlockers(IReadOnlyList<BillPurchaseBlocker> blockers) {
            if (blockers.Count == 0) return string.Empty;
            var lines = new List<string>(blockers.Count) { "Cannot purchase:" };
            for (int index = 0; index < blockers.Count; index++) {
                BillPurchaseBlocker blocker = blockers[index];
                lines.Add(blocker.Kind switch {
                    BillPurchaseBlockerKind.PendingSignature => "• Another bill awaits signature.",
                    BillPurchaseBlockerKind.ActiveLimitReached => "• Active bill limit reached.",
                    BillPurchaseBlockerKind.InsufficientFunds => $"• Missing {blocker.MissingFunds}$.",
                    BillPurchaseBlockerKind.RequirementNotMet =>
                        $"• {GetRequirementBlockerText(blocker.Requirement)}",
                    _ => "• Bill is currently unavailable."
                });
            }
            return string.Join("\n", lines);
        }

        private string GetRequirementBlockerText(BillRequirementSnapshot requirement) {
            if (requirement != null &&
                _bills.TryGetRequirementPresentation(requirement.TemplateId, out BillRequirementPresentationInfo info)) {
                if (!string.IsNullOrWhiteSpace(info.ShortDescription)) return info.ShortDescription;
                if (!string.IsNullOrWhiteSpace(info.DisplayName)) return $"Meet requirement: {info.DisplayName}.";
            }
            return GetRequirementDescription(requirement);
        }

        private static string GetRequirementName(BillRequirementKind kind) => kind switch {
            BillRequirementKind.OwnedUpgrade => "Upgrade",
            BillRequirementKind.MinimumClerkCount => "Clerks",
            BillRequirementKind.MinimumUnlockedDocumentQuality => "Quality",
            _ => "Requirement"
        };

        private static string GetRequirementDescription(BillRequirementSnapshot requirement) => requirement == null
            ? "Meet this requirement."
            : requirement.Kind switch {
            BillRequirementKind.OwnedUpgrade => $"Own upgrade: {requirement.UpgradeId}",
            BillRequirementKind.MinimumClerkCount => $"Have at least {requirement.NumericTarget} clerks.",
            BillRequirementKind.MinimumUnlockedDocumentQuality =>
                $"Unlock document quality {requirement.NumericTarget}.",
            _ => "Meet this requirement."
        };

        private static Color GetRequirementColor(BillRequirementKind kind) => kind switch {
            BillRequirementKind.OwnedUpgrade => new Color(0.35f, 0.65f, 1f),
            BillRequirementKind.MinimumClerkCount => new Color(0.95f, 0.65f, 0.2f),
            BillRequirementKind.MinimumUnlockedDocumentQuality => new Color(0.55f, 0.85f, 0.45f),
            _ => Color.white
        };

        private static string FormatDuration(double seconds) {
            TimeSpan duration = TimeSpan.FromSeconds(Math.Max(0d, seconds));
            return duration.TotalHours >= 1d
                ? $"{(int)duration.TotalHours:0}:{duration.Minutes:00}:{duration.Seconds:00}"
                : $"{duration.Minutes:0}:{duration.Seconds:00}";
        }
    }
}
