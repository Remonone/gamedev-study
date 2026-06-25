using System;
using Newtonsoft.Json.Linq;
using R3;
using Save;
using Types;
using Types.Enums;
using Types.Modifiers;
using Types.Research;
using Types.Values;
using UnityEngine;

namespace Services {
    public class ResearchService : IService, IStartable, IDisposable, ISaveable {
        private const string _ArchiveUnlockId = nameof(GovernmentInteractionType.Archive);
        private const double _DefaultBaseResearchCost = 100d;
        private const float _DefaultCostPerResearchMultiplier = 1f;
        private const float _DefaultArchiveInfluenceToPointsPerSecond = 1f;
        private const float _DefaultScaleModifier = 1f;
        private const string _DefaultNotificationTitle = "Research complete";
        private const string _DefaultNotificationMessage = "A research iteration is ready to claim.";

        private readonly SessionContext _sessionContext;
        private readonly UnlockService _unlockService;
        private readonly NotificationService _notificationService;
        private readonly ResearchConfig _config;
        private readonly CompositeDisposable _disposable = new();
        private readonly ReactiveProperty<ResearchState> _state;

        private int _completedCount;
        private Value _investedPoints = Value.Zero;
        private bool _readyNotificationSent;

        public ResearchService(
            SessionContext sessionContext,
            UnlockService unlockService,
            NotificationService notificationService,
            ResearchConfig config) {
            _sessionContext = sessionContext;
            _unlockService = unlockService;
            _notificationService = notificationService;
            _config = config;
            _state = new ReactiveProperty<ResearchState>(BuildState());
        }

        public Observable<ResearchState> State => _state;

        public string SaveKey => "Research";
        public int Priority => 70;

        public void StartService() {
            _unlockService.ObserveItemUnlocked(_ArchiveUnlockId)
                .Subscribe(_ => PublishState())
                .AddTo(_disposable);

            Observable.EveryUpdate()
                .Subscribe(_ => Tick(Time.deltaTime))
                .AddTo(_disposable);
        }

        public void CompleteCurrentResearch() {
            var state = BuildState();
            if (!state.CanComplete) {
                return;
            }

            _completedCount++;
            _investedPoints = Value.Zero;
            _readyNotificationSent = false;
            PublishState();
        }

        private void Tick(float deltaTime) {
            if (deltaTime <= 0f || !_unlockService.IsItemUnlocked(_ArchiveUnlockId)) {
                return;
            }

            var nextCost = CalculateNextCost();
            if (_investedPoints >= nextCost) {
                NotifyReadyIfNeeded();
                return;
            }

            var pointsPerSecond = CalculatePointsPerSecond();
            if (pointsPerSecond <= Value.Zero) {
                PublishState();
                return;
            }

            _investedPoints += pointsPerSecond * deltaTime;
            if (_investedPoints > nextCost) {
                _investedPoints = nextCost;
            }

            PublishState();

            if (_investedPoints >= nextCost) {
                NotifyReadyIfNeeded();
            }
        }

        private void NotifyReadyIfNeeded() {
            if (_readyNotificationSent) {
                return;
            }

            _readyNotificationSent = true;
            _notificationService.Push(new NotificationRequest(
                GetNotificationTitle(),
                GetNotificationMessage(),
                NotificationType.Info,
                NotificationPriority.High));
            PublishState();
        }

        private void PublishState() {
            _state.Value = BuildState();
        }

        private ResearchState BuildState() {
            return new ResearchState(
                _completedCount,
                _investedPoints,
                CalculateNextCost(),
                CalculatePointsPerSecond(),
                GetScaleModifier(),
                _unlockService.IsItemUnlocked(_ArchiveUnlockId));
        }

        private Value CalculateNextCost() {
            var multiplier = (_completedCount + 1) * GetCostPerResearchMultiplier() * GetScaleModifier();
            return GetBaseResearchCost() * multiplier;
        }

        private Value CalculatePointsPerSecond() {
            var archiveInfluence = Mathf.Max(0, _sessionContext.GetInfluenceValue(GovernmentInteractionType.Archive));
            return new Value(archiveInfluence * GetArchiveInfluenceToPointsPerSecond());
        }

        private Value GetBaseResearchCost() {
            return _config != null ? _config.BaseResearchCost : new Value(_DefaultBaseResearchCost);
        }

        private float GetCostPerResearchMultiplier() {
            return _config != null ? _config.CostPerResearchMultiplier : _DefaultCostPerResearchMultiplier;
        }

        private float GetArchiveInfluenceToPointsPerSecond() {
            return _config != null ? _config.ArchiveInfluenceToPointsPerSecond : _DefaultArchiveInfluenceToPointsPerSecond;
        }

        private float GetScaleModifier() {
            return _config != null ? _config.ScaleModifier : _DefaultScaleModifier;
        }

        private string GetNotificationTitle() {
            return _config != null ? _config.ReadyNotificationTitle : _DefaultNotificationTitle;
        }

        private string GetNotificationMessage() {
            return _config != null ? _config.ReadyNotificationMessage : _DefaultNotificationMessage;
        }

        public JToken Save() {
            return new JObject(
                new JProperty("CompletedCount", _completedCount),
                new JProperty("InvestedPoints", SaveValue(_investedPoints)),
                new JProperty("ReadyNotificationSent", _readyNotificationSent));
        }

        public void Load(JToken data) {
            if (data == null) {
                PublishState();
                return;
            }

            _completedCount = Math.Max(0, data.Value<int?>("CompletedCount") ?? 0);
            _investedPoints = LoadValue(data["InvestedPoints"]);
            _readyNotificationSent = data.Value<bool?>("ReadyNotificationSent") ?? BuildState().CanComplete;
            PublishState();
        }

        private static JObject SaveValue(Value value) {
            return new JObject(
                new JProperty("stored", value.Stored),
                new JProperty("degree", value.Base.Degree));
        }

        private static Value LoadValue(JToken token) {
            if (token == null || token.Type == JTokenType.Null) {
                return Value.Zero;
            }

            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float) {
                return new Value(token.Value<double>());
            }

            if (token is not JObject valueObject) {
                return Value.Zero;
            }

            var stored = valueObject.Value<double?>("stored") ?? 0d;
            var degree = valueObject.Value<int?>("degree") ?? 0;
            return new Value(stored, new Base { Degree = degree });
        }

        public void Dispose() {
            _disposable.Dispose();
            _state.Dispose();
        }
    }
}
