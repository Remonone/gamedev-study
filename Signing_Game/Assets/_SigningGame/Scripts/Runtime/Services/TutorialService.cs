using System;
using System.Collections.Generic;
using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Data.Tutorial;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using R3;
using Services.Locator;
using UnityEngine;

namespace Services {
    public sealed class TutorialService : IService, IInitialize, ISaveable {
        private sealed class Entry {
            public TutorialDefinition Definition;
            public bool LastSatisfied;
            public bool Completed;
        }

        private readonly List<Entry> _entries = new();
        private readonly HashSet<string> _completedIds = new(StringComparer.Ordinal);
        private readonly List<string> _pendingActivationIds = new();
        private readonly CompositeDisposable _subscriptions = new();
        private readonly Subject<Unit> _changed = new();

        private IAssetProvider _assetProvider;
        private IAssetListLease<TutorialDefinition> _lease;
        private GameStatisticsService _statistics;
        private UpgradeService _upgrades;
        private TutorialTriggerContext _context;
        private bool _definitionsBuilt;

        private TutorialDefinition _activeDefinition;
        private int _slideIndex;
        private bool _slideTypingCompleted;

        public string SaveId => "Tutorial";
        public Observable<Unit> Changed => _changed;

        public bool HasActive => _activeDefinition != null;
        public TutorialDefinition ActiveDefinition => _activeDefinition;
        public int SlideIndex => _slideIndex;

        public TutorialService() { }

        internal TutorialService(IAssetProvider assetProvider) {
            _assetProvider = assetProvider ?? throw new ArgumentNullException(nameof(assetProvider));
        }

        public async UniTask InitializeAsync(IServiceScope scope) {
            _statistics = scope.Get<GameStatisticsService>();
            _upgrades = scope.Get<UpgradeService>();
            _context = new TutorialTriggerContext(_statistics, _upgrades);

            if (!_definitionsBuilt && _assetProvider == null && scope.Container != null) {
                scope.Container.TryGet(out _assetProvider);
            }

            if (!_definitionsBuilt && _assetProvider != null) {
                _lease = await _assetProvider.LoadAssetsByLabelAsync<TutorialDefinition>(
                    AddressableConstants.TUTORIAL_LABEL);
                BuildEntries(_lease.Assets);
            }

            ApplyCompletedFlags();
            CaptureBaseline();
            _statistics.Changed.Subscribe(_ => ReevaluateTriggers()).AddTo(_subscriptions);
            _upgrades.Changed.Subscribe(_ => ReevaluateTriggers()).AddTo(_subscriptions);
        }

        internal void SetDefinitions(IReadOnlyList<TutorialDefinition> definitions) {
            BuildEntries(definitions);
        }

        internal void BuildEntries(IReadOnlyList<TutorialDefinition> assets) {
            if (assets == null) throw new ArgumentNullException(nameof(assets));

            _entries.Clear();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (TutorialDefinition definition in assets) {
                if (!IsValidDefinition(definition, out string reason)) {
                    Debug.LogWarning($"Tutorial definition '{(definition != null ? definition.name : "<null>")}' " +
                                     $"was excluded: {reason}");
                    continue;
                }

                if (!seenIds.Add(definition.Id)) {
                    Debug.LogWarning($"Duplicate tutorial ID '{definition.Id}'; the later definition was excluded.");
                    continue;
                }

                _entries.Add(new Entry { Definition = definition });
            }

            _definitionsBuilt = true;
        }

        public void NotifyTypingCompleted() {
            if (_activeDefinition == null || _slideTypingCompleted) return;

            _slideTypingCompleted = true;
            _changed.OnNext(Unit.Default);
        }

        public void NotifyInteraction(in TutorialInteractionEvent interactionEvent) {
            if (_activeDefinition == null || !_slideTypingCompleted) return;

            TutorialSlideCondition condition = CurrentSlideCondition;
            if (condition == null || !condition.IsSatisfiedBy(in interactionEvent)) return;

            AdvanceSlide();
        }

        private TutorialSlideCondition CurrentSlideCondition =>
            _activeDefinition != null && _slideIndex < _activeDefinition.Slides.Count
                ? _activeDefinition.Slides[_slideIndex].AdvanceCondition
                : null;

        private void AdvanceSlide() {
            _slideIndex++;
            if (_slideIndex >= _activeDefinition.Slides.Count) {
                CompleteActive();
                return;
            }

            _slideTypingCompleted = false;
            _changed.OnNext(Unit.Default);
        }

        private void CompleteActive() {
            Entry entry = FindEntry(_activeDefinition.Id);
            if (entry != null && !entry.Completed) {
                entry.Completed = true;
                _completedIds.Add(entry.Definition.Id);
            }

            _activeDefinition = null;
            _slideIndex = 0;
            _slideTypingCompleted = false;
            TryActivateNextPending();
            _changed.OnNext(Unit.Default);
        }

        private void CaptureBaseline() {
            if (_context == null) return;

            for (int index = 0; index < _entries.Count; index++) {
                _entries[index].LastSatisfied = _entries[index].Definition.Trigger.IsSatisfied(_context);
            }
        }

        private void ReevaluateTriggers() {
            if (_context == null) return;

            for (int index = 0; index < _entries.Count; index++) {
                Entry entry = _entries[index];
                bool satisfied = entry.Definition.Trigger.IsSatisfied(_context);
                bool becameSatisfied = !entry.LastSatisfied && satisfied;
                entry.LastSatisfied = satisfied;

                if (!becameSatisfied || entry.Completed) continue;
                if (_activeDefinition != null &&
                    string.Equals(_activeDefinition.Id, entry.Definition.Id, StringComparison.Ordinal)) {
                    continue;
                }

                EnqueuePending(entry.Definition.Id);
            }

            if (_activeDefinition == null) TryActivateNextPending();
        }

        private void EnqueuePending(string definitionId) {
            if (!_pendingActivationIds.Contains(definitionId)) _pendingActivationIds.Add(definitionId);
        }

        private void TryActivateNextPending() {
            for (int index = 0; index < _pendingActivationIds.Count;) {
                string pendingId = _pendingActivationIds[index];
                Entry entry = FindEntry(pendingId);
                if (entry == null || entry.Completed || !entry.Definition.Trigger.IsSatisfied(_context)) {
                    _pendingActivationIds.RemoveAt(index);
                    continue;
                }

                _pendingActivationIds.RemoveAt(index);
                Activate(entry);
                return;
            }
        }

        private void Activate(Entry entry) {
            if (_activeDefinition != null || entry.Completed) return;

            _activeDefinition = entry.Definition;
            _slideIndex = 0;
            _slideTypingCompleted = false;
            _changed.OnNext(Unit.Default);
        }

        private Entry FindEntry(string definitionId) {
            for (int index = 0; index < _entries.Count; index++) {
                if (string.Equals(_entries[index].Definition.Id, definitionId, StringComparison.Ordinal)) {
                    return _entries[index];
                }
            }

            return null;
        }

        private void ApplyCompletedFlags() {
            for (int index = 0; index < _entries.Count; index++) {
                _entries[index].Completed = _completedIds.Contains(_entries[index].Definition.Id);
            }
        }

        private static bool IsValidDefinition(TutorialDefinition definition, out string reason) {
            reason = null;
            if (definition == null) {
                reason = "the asset is null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(definition.Id)) {
                reason = "the ID is empty.";
                return false;
            }

            if (definition.Trigger == null) {
                reason = "no trigger is assigned.";
                return false;
            }

            if (definition.Slides == null || definition.Slides.Count == 0) {
                reason = "there are no slides.";
                return false;
            }

            for (int index = 0; index < definition.Slides.Count; index++) {
                TutorialSlide slide = definition.Slides[index];
                if (slide == null || string.IsNullOrWhiteSpace(slide.Text)) {
                    reason = $"slide {index} has no text.";
                    return false;
                }

                if (slide.AdvanceCondition == null) {
                    reason = $"slide {index} has no advance condition.";
                    return false;
                }
            }

            return true;
        }

        public JToken Serialize() {
            var completed = new JArray();
            foreach (string id in _completedIds) completed.Add(id);
            return new JObject {
                ["completed"] = completed
            };
        }

        public void Deserialize(JToken state) {
            if (state is not JObject data) {
                throw new JsonSerializationException("Tutorial state must be an object.");
            }

            JToken completedToken = data["completed"];
            if (completedToken != null && completedToken is not JArray) {
                throw new JsonSerializationException("Tutorial completed state must be an array.");
            }

            var restored = new HashSet<string>(StringComparer.Ordinal);
            if (completedToken is JArray completedArray) {
                foreach (JToken token in completedArray) {
                    if (token.Type != JTokenType.String) {
                        throw new JsonSerializationException("Tutorial completed entries must be strings.");
                    }

                    string id = token.Value<string>();
                    if (string.IsNullOrWhiteSpace(id)) {
                        throw new JsonSerializationException("Tutorial completed entries cannot be empty.");
                    }

                    if (!restored.Add(id)) {
                        throw new JsonSerializationException($"Duplicate tutorial completed ID '{id}'.");
                    }
                }
            }

            if (_completedIds.SetEquals(restored)) return;

            _completedIds.Clear();
            foreach (string id in restored) _completedIds.Add(id);
            ApplyCompletedFlags();
        }

        public void Dispose() {
            _subscriptions.Dispose();
            _changed.Dispose();
            _entries.Clear();
            _pendingActivationIds.Clear();
            _lease?.Dispose();
            _lease = null;
            _activeDefinition = null;
            _statistics = null;
            _upgrades = null;
            _context = null;
        }
    }
}
