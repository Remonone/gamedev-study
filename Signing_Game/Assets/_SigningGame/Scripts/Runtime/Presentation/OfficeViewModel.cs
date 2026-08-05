using System;
using System.Collections.Generic;
using Data.Office;
using R3;
using Services;
using Utils;

namespace Presentation {
    public sealed class OfficeViewModel : IDisposable {
        private const double MaximumFiniteStored = 999.999999d;

        private readonly OfficeService _office;
        private readonly WalletService _wallet;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly ReactiveProperty<bool> _availability = new(false);
        private readonly Subject<Unit> _summaryChanged = new();
        private readonly Subject<Unit> _slotsChanged = new();
        private readonly Subject<Unit> _bidChanged = new();
        private readonly List<OfficeSlotPresentationModel> _slots = new();

        private Value _committedBid = Value.One;
        private Value _previewBid = Value.One;
        private Value _currentBalance;
        private float _bidSliderValue;
        private bool _isBidEditing;
        private bool _summaryInitialized;
        private int _clerkCount;
        private int _capacity;
        private int _pendingHireCount;
        private int _pendingSalaryReviewCount;
        private float _documentsPerSecondPerClerk;
        private float _qualityCeiling;
        private float _acceptanceThreshold;
        private float _rewardMultiplier;

        public ReadOnlyReactiveProperty<bool> Availability => _availability;
        public Observable<Unit> SummaryChanged => _summaryChanged;
        public Observable<Unit> SlotsChanged => _slotsChanged;
        public Observable<Unit> BidChanged => _bidChanged;
        public IReadOnlyList<OfficeSlotPresentationModel> Slots => _slots;

        public int ClerkCount => _clerkCount;
        public int Capacity => _capacity;
        public int PendingHireCount => _pendingHireCount;
        public int PendingSalaryReviewCount => _pendingSalaryReviewCount;
        public float DocumentsPerSecondPerClerk => _documentsPerSecondPerClerk;
        public float QualityCeiling => _qualityCeiling;
        public float AcceptanceThreshold => _acceptanceThreshold;
        public float RewardMultiplier => _rewardMultiplier;
        public Value CommittedBid => _committedBid;
        public Value PreviewBid => _previewBid;
        public Value CurrentBalance => _currentBalance;
        public float BidSliderValue => _bidSliderValue;
        public bool IsBidEditing => _isBidEditing;
        public bool CanConfirmBid => _isBidEditing && !_previewBid.IsZero;

        public OfficeViewModel(OfficeService office, WalletService wallet) {
            _office = office ?? throw new ArgumentNullException(nameof(office));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _office.Changed.Subscribe(_ => RefreshFromOffice()).AddTo(_subscriptions);
            RefreshFromOffice();
        }

        public bool BeginBidEdit() {
            if (!HasPurchaseSlot()) return false;
            _isBidEditing = true;
            Value balance = _wallet.CurrentBalance;
            if (balance.IsZero) {
                _bidSliderValue = 0f;
                _previewBid = Value.Zero;
            }
            else {
                _bidSliderValue = ResolveNormalizedBid(_committedBid, balance);
                _previewBid = ResolveBid(_bidSliderValue, balance);
            }

            _bidChanged.OnNext(Unit.Default);
            return true;
        }

        public void SetBidSliderValue(float normalized) {
            if (!_isBidEditing) return;
            _bidSliderValue = float.IsNaN(normalized) || float.IsInfinity(normalized)
                ? 0f
                : Math.Clamp(normalized, 0f, 1f);
            Value next = ResolveBid(_bidSliderValue, _wallet.CurrentBalance);
            if (next == _previewBid) return;
            _previewBid = next;
            _bidChanged.OnNext(Unit.Default);
        }

        public bool ConfirmBidEdit() {
            if (!CanConfirmBid) return false;
            _committedBid = _previewBid;
            _isBidEditing = false;
            _bidChanged.OnNext(Unit.Default);
            RefreshSlotsIfNeeded();
            return true;
        }

        public void CancelBidEdit() {
            if (!_isBidEditing) return;
            _isBidEditing = false;
            _previewBid = _committedBid;
            _bidChanged.OnNext(Unit.Default);
        }

        public bool TryHire() {
            return _office.TryStartClerkHire(_committedBid);
        }

        public bool TryReviewSalary(int clerkId) {
            return _office.TryStartSalaryReview(clerkId);
        }

        public bool TryDismiss(int clerkId) {
            return _office.TryDismissClerk(clerkId);
        }

        public void Dispose() {
            _subscriptions.Dispose();
            _availability.Dispose();
            _summaryChanged.Dispose();
            _slotsChanged.Dispose();
            _bidChanged.Dispose();
            _slots.Clear();
        }

        internal static Value ResolveBid(float normalized, Value balance) {
            if (balance.IsZero) return Value.Zero;
            Value maximum = ResolveMaximumSelectable(balance);
            Value minimum = maximum < Value.One ? maximum : Value.One;
            if (maximum == minimum) return maximum;

            double unit = Math.Clamp((double)normalized, 0d, 1d);
            if (unit <= 0d) return minimum;
            if (unit >= 1d) return maximum;
            double minimumLog = minimum.ToLog10();
            double maximumLog = maximum.ToLog10();
            return Value.FromLog10(minimumLog + (maximumLog - minimumLog) * unit);
        }

        internal static float ResolveNormalizedBid(Value bid, Value balance) {
            if (balance.IsZero || bid.IsZero) return 0f;
            Value maximum = ResolveMaximumSelectable(balance);
            Value minimum = maximum < Value.One ? maximum : Value.One;
            if (maximum == minimum || bid <= minimum) return 0f;
            if (bid >= maximum) return 1f;

            double minimumLog = minimum.ToLog10();
            double maximumLog = maximum.ToLog10();
            double normalized = (bid.ToLog10() - minimumLog) / (maximumLog - minimumLog);
            return (float)Math.Clamp(normalized, 0d, 1d);
        }

        internal static Value ResolveMaximumSelectable(Value balance) {
            return balance.Base.Degree == int.MaxValue
                ? new Value(MaximumFiniteStored, new BaseValue(int.MaxValue - 1))
                : balance;
        }

        private void RefreshFromOffice() {
            bool available = _office.IsUnlocked;
            if (_availability.Value != available) _availability.Value = available;

            Value nextBalance = _wallet.CurrentBalance;
            bool bidDisplayChanged = nextBalance != _currentBalance;
            _currentBalance = nextBalance;

            bool summaryChanged = !_summaryInitialized ||
                                  _clerkCount != _office.ClerkCount ||
                                  _capacity != _office.ClerkCapacity ||
                                  _pendingHireCount != _office.PendingHireCount ||
                                  _pendingSalaryReviewCount != _office.PendingSalaryReviewCount ||
                                  !_documentsPerSecondPerClerk.Equals(_office.DocumentsPerSecondPerClerk) ||
                                  !_qualityCeiling.Equals(_office.QualityCeiling) ||
                                  !_acceptanceThreshold.Equals(_office.AcceptanceThreshold) ||
                                  !_rewardMultiplier.Equals(_office.RewardMultiplier);
            if (summaryChanged) {
                _summaryInitialized = true;
                _clerkCount = _office.ClerkCount;
                _capacity = _office.ClerkCapacity;
                _pendingHireCount = _office.PendingHireCount;
                _pendingSalaryReviewCount = _office.PendingSalaryReviewCount;
                _documentsPerSecondPerClerk = _office.DocumentsPerSecondPerClerk;
                _qualityCeiling = _office.QualityCeiling;
                _acceptanceThreshold = _office.AcceptanceThreshold;
                _rewardMultiplier = _office.RewardMultiplier;
                _summaryChanged.OnNext(Unit.Default);
            }

            if (_isBidEditing) {
                Value nextPreview = ResolveBid(_bidSliderValue, _currentBalance);
                if (nextPreview != _previewBid) {
                    _previewBid = nextPreview;
                    bidDisplayChanged = true;
                }
            }

            if (bidDisplayChanged) _bidChanged.OnNext(Unit.Default);
            RefreshSlotsIfNeeded();
        }

        private void RefreshSlotsIfNeeded() {
            int totalSlots = Math.Max(_office.ClerkCapacity, _office.ClerkCount);
            bool hasPurchase = (long)_office.ClerkCount + _office.PendingHireCount < _office.ClerkCapacity;
            if (!NeedsSlotRebuild(totalSlots, hasPurchase)) return;

            _slots.Clear();
            IReadOnlyList<OfficeClerkState> clerks = _office.Clerks;
            for (int index = 0; index < clerks.Count; index++) {
                OfficeClerkState clerk = clerks[index];
                bool pending = _office.HasPendingSalaryReview(clerk.Id);
                _slots.Add(OfficeSlotPresentationModel.Clerk(
                    clerk.Id,
                    clerk.Name,
                    clerk.Age,
                    clerk.BaseEfficiency,
                    clerk.BonusEfficiency,
                    _office.GetSalaryReviewCost(clerk.Id),
                    _office.CanStartSalaryReview(clerk.Id),
                    pending));
            }

            for (int index = clerks.Count; index < totalSlots; index++) {
                _slots.Add(hasPurchase && index == clerks.Count
                    ? OfficeSlotPresentationModel.Purchase(
                        _committedBid,
                        _office.CanStartClerkHire(_committedBid))
                    : OfficeSlotPresentationModel.Vacant());
            }

            _slotsChanged.OnNext(Unit.Default);
        }

        private bool NeedsSlotRebuild(int totalSlots, bool hasPurchase) {
            if (_slots.Count != totalSlots) return true;
            IReadOnlyList<OfficeClerkState> clerks = _office.Clerks;
            for (int index = 0; index < clerks.Count; index++) {
                if (index >= _slots.Count) return true;
                OfficeClerkState clerk = clerks[index];
                OfficeSlotPresentationModel model = _slots[index];
                if (model.State != OfficeSlotState.Clerk || model.ClerkId != clerk.Id ||
                    !string.Equals(model.Name, clerk.Name, StringComparison.Ordinal) || model.Age != clerk.Age ||
                    !model.BaseEfficiency.Equals(clerk.BaseEfficiency) ||
                    !model.BonusEfficiency.Equals(clerk.BonusEfficiency) ||
                    model.ReviewCost != _office.GetSalaryReviewCost(clerk.Id) ||
                    model.CanReview != _office.CanStartSalaryReview(clerk.Id) ||
                    model.IsReviewPending != _office.HasPendingSalaryReview(clerk.Id)) {
                    return true;
                }
            }

            for (int index = clerks.Count; index < totalSlots; index++) {
                OfficeSlotPresentationModel model = _slots[index];
                if (hasPurchase && index == clerks.Count) {
                    if (model.State != OfficeSlotState.Purchase || model.Bid != _committedBid ||
                        model.CanHire != _office.CanStartClerkHire(_committedBid)) {
                        return true;
                    }
                }
                else if (model.State != OfficeSlotState.Vacant) {
                    return true;
                }
            }

            return false;
        }

        private bool HasPurchaseSlot() {
            for (int index = 0; index < _slots.Count; index++) {
                if (_slots[index].State == OfficeSlotState.Purchase) return true;
            }

            return false;
        }
    }
}
