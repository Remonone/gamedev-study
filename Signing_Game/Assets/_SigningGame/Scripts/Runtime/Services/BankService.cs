using System;
using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Cache;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using R3;
using Services.Locator;
using UnityEngine;
using Utils;

namespace Services {
    public sealed class BankService : IService, IInitialize, IPostInitialize, ISaveable {
        public const int MaxPayoutsPerTick = 256;

        private const double MaximumValueLog10 = (double)int.MaxValue * 3d;

        private readonly CompositeDisposable _subscriptions = new();
        private readonly Func<float> _randomValue;
        private readonly Observable<float> _updateStream;

        private UnlockService _unlocks;
        private WalletService _wallet;
        private IReadOnlyCacheData<BankEntries> _bankData;
        private double? _pendingElapsedSeconds;
        private double _elapsedSeconds;
        private bool _initialized;
        private bool _updateSubscribed;
        private bool _isTicking;

        public string SaveId => "bank";
        public bool IsUnlocked => _unlocks?.IsUnlocked(FeatureIds.Bank) ?? false;
        public double ElapsedSeconds => _elapsedSeconds;

        public BankService() : this(null, null) { }

        internal BankService(Func<float> randomValue, Observable<float> updateStream) {
            _randomValue = randomValue ?? (() => UnityEngine.Random.value);
            _updateStream = updateStream;
        }

        public UniTask InitializeAsync(IServiceScope scope) {
            _unlocks = scope.Get<UnlockService>();
            _wallet = scope.Get<WalletService>();
            _bankData = scope.Get<PlayerStatStash>().BankData;
            if (_bankData == null) {
                throw new InvalidOperationException(
                    "BankService requires a registered BankEntries cache calculator.");
            }

            _initialized = true;
            if (_pendingElapsedSeconds.HasValue) {
                _elapsedSeconds = _pendingElapsedSeconds.Value;
                _pendingElapsedSeconds = null;
            }

            return UniTask.CompletedTask;
        }

        public UniTask PostInitializeAsync(IServiceScope scope) {
            if (_updateSubscribed) return UniTask.CompletedTask;
            _updateSubscribed = true;
            Observable<float> stream = _updateStream ?? Observable.EveryUpdate().Select(_ => Time.deltaTime);
            stream.Subscribe(Tick).AddTo(_subscriptions);
            return UniTask.CompletedTask;
        }

        public void Tick(float deltaTime) {
            if (!_initialized || _isTicking || !IsUnlocked || float.IsNaN(deltaTime) ||
                float.IsInfinity(deltaTime) || deltaTime <= 0f) {
                return;
            }

            _isTicking = true;
            try {
                BankEntries entries = _bankData.Value;
                double interval = entries.PayoutIntervalSeconds;
                double nextElapsed = SaturatingAddSeconds(_elapsedSeconds, deltaTime);
                int payoutCount = CountPayouts(nextElapsed, interval);
                if (payoutCount <= 0) {
                    _elapsedSeconds = nextElapsed;
                    return;
                }

                Value payout = Value.Zero;
                for (int index = 0; index < payoutCount; index++) {
                    int guaranteedExtra = MultiPayUtility.SplitChance(entries.MultiPayChance, out float extraChance);
                    int paymentCount = 1 + guaranteedExtra;
                    if (extraChance > 0f && SampleRandomUnit() < extraChance) paymentCount++;

                    for (int paymentIndex = 0; paymentIndex < paymentCount; paymentIndex++) {
                        double randomUnit = SampleRandomUnit();
                        bool isCritical = entries.CriticalChance >= 1f ||
                                          entries.CriticalChance > 0f && randomUnit < entries.CriticalChance;
                        double multiplier = isCritical
                            ? entries.CriticalMultiplier
                            : 1d;
                        Value intervalPayout = MultiplyValueSafely(entries.PayoutAmount, multiplier);
                        payout = AddValuesSafely(payout, intervalPayout);
                    }
                }

                double consumed = interval * payoutCount;
                double retained = nextElapsed - consumed;
                _elapsedSeconds = retained >= 0d && !double.IsNaN(retained) ? retained : 0d;
                CreditWallet(payout, true);
            }
            finally {
                _isTicking = false;
            }
        }

        internal Value ApplyBillCostCompensation(Value actuallyDebited) {
            if (!_initialized || !IsUnlocked || actuallyDebited.IsZero) return Value.Zero;

            double ratio = _bankData.Value.BillCostCompensationRatio;
            if (ratio <= 0d) return Value.Zero;
            Value compensation = ratio >= 1d
                ? actuallyDebited
                : MultiplyValueSafely(actuallyDebited, ratio);
            return CreditWallet(compensation, false);
        }

        public JToken Serialize() {
            return new JObject { ["elapsedSeconds"] = _elapsedSeconds };
        }

        public void Deserialize(JToken state) {
            if (_isTicking) throw new InvalidOperationException("Bank state cannot be restored during a bank tick.");
            if (state is not JObject data || !TryReadNumber(data["elapsedSeconds"], out double elapsedSeconds) ||
                double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0d) {
                throw new JsonSerializationException(
                    "Bank save data must contain a finite, non-negative elapsedSeconds value.");
            }

            if (_initialized) _elapsedSeconds = elapsedSeconds;
            else _pendingElapsedSeconds = elapsedSeconds;
        }

        public void Dispose() {
            _subscriptions.Dispose();
            _unlocks = null;
            _wallet = null;
            _bankData = null;
            _pendingElapsedSeconds = null;
            _initialized = false;
            _updateSubscribed = false;
        }

        private int CountPayouts(double elapsedSeconds, double interval) {
            if (elapsedSeconds < interval) return 0;
            double maximumCoveredSeconds = interval * MaxPayoutsPerTick;
            if (double.IsInfinity(maximumCoveredSeconds) || elapsedSeconds < maximumCoveredSeconds) {
                double quotient = Math.Floor(elapsedSeconds / interval);
                if (quotient <= 0d) return 0;
                return quotient >= MaxPayoutsPerTick ? MaxPayoutsPerTick : (int)quotient;
            }

            return MaxPayoutsPerTick;
        }

        private double SampleRandomUnit() {
            float sample = _randomValue();
            if (float.IsNaN(sample) || float.IsInfinity(sample)) return 0d;
            return Math.Clamp((double)sample, 0d, 1d);
        }

        private Value CreditWallet(Value amount, bool notify) {
            if (amount.IsZero) return Value.Zero;
            Value before = _wallet.CurrentBalance;
            if (!_wallet.ReplenishWallet(amount, notify)) return Value.Zero;
            Value after = _wallet.CurrentBalance;
            return after > before ? (after - before).Value : Value.Zero;
        }

        private static double SaturatingAddSeconds(double elapsedSeconds, float deltaTime) {
            double result = elapsedSeconds + deltaTime;
            return double.IsInfinity(result) ? double.MaxValue : result;
        }

        private static Value AddValuesSafely(Value first, Value second) {
            if (first.IsZero) return second;
            if (second.IsZero) return first;
            if (first == Value.Infinity || second == Value.Infinity) return Value.Infinity;

            try {
                Value result = first + second;
                return result.Base.Degree < 0 ? Value.Infinity : result;
            }
            catch (Exception) {
                return Value.Infinity;
            }
        }

        private static Value MultiplyValueSafely(Value value, double multiplier) {
            if (value.IsZero || multiplier <= 0d) return Value.Zero;
            if (multiplier == 1d) return value;
            if (double.IsNaN(multiplier)) return Value.Zero;
            if (double.IsPositiveInfinity(multiplier)) return Value.Infinity;

            double logarithm = value.ToLog10() + Math.Log10(multiplier);
            if (double.IsNaN(logarithm)) return Value.Zero;
            if (double.IsPositiveInfinity(logarithm) || logarithm >= MaximumValueLog10) return Value.Infinity;
            if (double.IsNegativeInfinity(logarithm)) return Value.Zero;

            try {
                return Value.FromLog10(logarithm);
            }
            catch (ArgumentException) {
                return Value.Infinity;
            }
        }

        private static bool TryReadNumber(JToken token, out double value) {
            if (token?.Type is JTokenType.Integer or JTokenType.Float) {
                value = token.Value<double>();
                return true;
            }

            value = default;
            return false;
        }
    }
}
