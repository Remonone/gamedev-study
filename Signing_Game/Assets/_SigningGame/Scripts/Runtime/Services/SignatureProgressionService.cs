using System;
using System.Collections.Generic;
using Authoring;
using Constants;
using Contracts;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using R3;
using Services.Locator;

namespace Services {
    public sealed class SignatureProgressionService : IService, ISaveable, IInitialize {
        public const string ProgressionSaveId = "signature_progression";
        private const int OfferCount = 3;
        private const string LegacyPresetId = "test_preset";

        private readonly GameLaunchMode _launchMode;
        private readonly Func<int, int> _nextIndex;
        private readonly List<string> _unlockedIds = new();
        private readonly List<string> _pendingOfferIds = new();
        private readonly Subject<string> _activePresetChanged = new();

        private ISignaturePresetRepository _repository;
        private bool _restoredSection;
        private string _activePresetId;

        public string SaveId => ProgressionSaveId;
        public string ActivePresetId => _activePresetId;
        public IReadOnlyList<string> UnlockedPresetIds => _unlockedIds;
        public IReadOnlyList<string> PendingOfferIds => _pendingOfferIds;
        public bool RequiresStartingSelection => string.IsNullOrWhiteSpace(_activePresetId);
        public Observable<string> ActivePresetChanged => _activePresetChanged;

        public SignatureProgressionService(GameLaunchMode launchMode)
            : this(launchMode, upperBound => UnityEngine.Random.Range(0, upperBound)) { }

        internal SignatureProgressionService(GameLaunchMode launchMode, Func<int, int> nextIndex) {
            _launchMode = launchMode;
            _nextIndex = nextIndex ?? throw new ArgumentNullException(nameof(nextIndex));
        }

        public UniTask InitializeAsync(IServiceScope scope) {
            _repository = scope.Get<ISignaturePresetRepository>();
            NormalizeRestoredState();
            return UniTask.CompletedTask;
        }

        public bool TrySelectStartingPreset(string presetId) {
            if (!RequiresStartingSelection || string.IsNullOrWhiteSpace(presetId) ||
                !_pendingOfferIds.Contains(presetId)) return false;
            if (!_repository.TryGetPreset(presetId, out _)) return false;

            _pendingOfferIds.Clear();
            _unlockedIds.Clear();
            _unlockedIds.Add(presetId);
            _activePresetId = presetId;
            _activePresetChanged.OnNext(presetId);
            return true;
        }

        public bool IsUnlocked(string presetId) => !string.IsNullOrWhiteSpace(presetId) && _unlockedIds.Contains(presetId);

        public bool TryGetPendingPreset(int index, out SignaturePresetDefinition preset) {
            if (index < 0 || index >= _pendingOfferIds.Count) {
                preset = null;
                return false;
            }
            return _repository.TryGetPreset(_pendingOfferIds[index], out preset);
        }

        public JToken Serialize() => new JObject {
            ["activePresetId"] = _activePresetId == null ? JValue.CreateNull() : new JValue(_activePresetId),
            ["unlockedPresetIds"] = new JArray(_unlockedIds),
            ["pendingOfferIds"] = new JArray(_pendingOfferIds)
        };

        public void Deserialize(JToken state) {
            if (state is not JObject root) throw new JsonSerializationException("Signature progression must be an object.");

            string activeId = null;
            JToken activeToken = root["activePresetId"];
            if (activeToken != null && activeToken.Type != JTokenType.Null) {
                if (activeToken.Type != JTokenType.String) throw new JsonSerializationException("activePresetId must be a string or null.");
                activeId = activeToken.Value<string>();
            }

            List<string> unlocked = ParseStringArray(root["unlockedPresetIds"], "unlockedPresetIds");
            List<string> pending = ParseStringArray(root["pendingOfferIds"], "pendingOfferIds");

            _activePresetId = activeId;
            _unlockedIds.Clear();
            _unlockedIds.AddRange(unlocked);
            _pendingOfferIds.Clear();
            _pendingOfferIds.AddRange(pending);
            _restoredSection = true;
        }

        public void Dispose() {
            _activePresetChanged.Dispose();
            _repository = null;
            _unlockedIds.Clear();
            _pendingOfferIds.Clear();
            _activePresetId = null;
        }

        private void NormalizeRestoredState() {
            var validUnlocked = new List<string>(_unlockedIds.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < _unlockedIds.Count; index++) {
                string id = _unlockedIds[index];
                if (seen.Add(id) && _repository.TryGetPreset(id, out _)) validUnlocked.Add(id);
            }
            _unlockedIds.Clear();
            _unlockedIds.AddRange(validUnlocked);

            if (!string.IsNullOrWhiteSpace(_activePresetId) && _repository.TryGetPreset(_activePresetId, out _)) {
                if (!_unlockedIds.Contains(_activePresetId)) _unlockedIds.Add(_activePresetId);
            } else {
                _activePresetId = _unlockedIds.Count > 0 ? _unlockedIds[0] : null;
            }

            if (!string.IsNullOrWhiteSpace(_activePresetId)) {
                _pendingOfferIds.Clear();
                return;
            }

            if (_restoredSection && HasValidPendingOffers()) return;

            if (!_restoredSection && _launchMode == GameLaunchMode.Continue) {
                if (_repository.TryGetPreset(LegacyPresetId, out SignaturePresetDefinition legacy)) {
                    SetLegacyActive(legacy.Id);
                    return;
                }

                SignaturePresetDefinition fallback = FindFirstStarter();
                if (fallback != null) {
                    SetLegacyActive(fallback.Id);
                    return;
                }
            }

            GeneratePendingOffers();
        }

        private bool HasValidPendingOffers() {
            if (_pendingOfferIds.Count != OfferCount) return false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < _pendingOfferIds.Count; index++) {
                string id = _pendingOfferIds[index];
                if (!seen.Add(id) || !_repository.TryGetPreset(id, out SignaturePresetDefinition preset) ||
                    !preset.HasTag(InternalConstants.STARTING_SIGNATURE_TAG)) return false;
            }
            return true;
        }

        private void GeneratePendingOffers() {
            var candidates = new List<SignaturePresetDefinition>();
            IReadOnlyList<SignaturePresetDefinition> presets = _repository.Presets;
            for (int index = 0; index < presets.Count; index++) {
                SignaturePresetDefinition preset = presets[index];
                if (preset != null && preset.HasTag(InternalConstants.STARTING_SIGNATURE_TAG)) candidates.Add(preset);
            }
            if (candidates.Count < OfferCount) {
                throw new InvalidOperationException(
                    $"At least {OfferCount} signature presets tagged '{InternalConstants.STARTING_SIGNATURE_TAG}' are required.");
            }

            _pendingOfferIds.Clear();
            for (int offerIndex = 0; offerIndex < OfferCount; offerIndex++) {
                int remaining = candidates.Count - offerIndex;
                int offset = _nextIndex(remaining);
                if (offset < 0 || offset >= remaining) {
                    throw new InvalidOperationException($"Signature random source returned {offset} for upper bound {remaining}.");
                }
                int selectedIndex = offerIndex + offset;
                (candidates[offerIndex], candidates[selectedIndex]) = (candidates[selectedIndex], candidates[offerIndex]);
                _pendingOfferIds.Add(candidates[offerIndex].Id);
            }
        }

        private SignaturePresetDefinition FindFirstStarter() {
            IReadOnlyList<SignaturePresetDefinition> presets = _repository.Presets;
            for (int index = 0; index < presets.Count; index++) {
                if (presets[index] != null && presets[index].HasTag(InternalConstants.STARTING_SIGNATURE_TAG)) return presets[index];
            }
            return null;
        }

        private void SetLegacyActive(string id) {
            _activePresetId = id;
            _unlockedIds.Clear();
            _unlockedIds.Add(id);
            _pendingOfferIds.Clear();
        }

        private static List<string> ParseStringArray(JToken token, string propertyName) {
            if (token is not JArray array) throw new JsonSerializationException($"{propertyName} must be an array.");
            var result = new List<string>(array.Count);
            foreach (JToken item in array) {
                if (item.Type != JTokenType.String) throw new JsonSerializationException($"{propertyName} must contain strings.");
                result.Add(item.Value<string>());
            }
            return result;
        }
    }
}
