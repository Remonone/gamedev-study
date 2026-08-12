using System;
using System.Collections.Generic;
using System.Globalization;
using Data.Modifiers;
using Data.Research;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Services {
    public sealed partial class ResearchService {
        public JToken Serialize() {
            var offers = new JArray();
            for (int index = 0; index < _offers.Count; index++) offers.Add(_offers[index].Id);
            JToken pending = JValue.CreateNull();
            if (_pending.HasValue) {
                pending = new JObject {
                    ["practiceId"] = _pending.Value.PracticeId,
                    ["signatureThreshold"] = _pending.Value.FrozenSignatureThreshold
                };
            }
            var active = new JArray();
            for (int index = 0; index < _active.Count; index++) {
                ActivePracticeState state = _active[index];
                active.Add(new JObject {
                    ["practiceId"] = state.Definition.Id,
                    ["effectiveness"] = state.Effectiveness,
                    ["permanent"] = state.IsPermanent,
                    ["remainingSeconds"] = state.IsPermanent ? JValue.CreateNull() : state.RemainingSeconds
                });
            }
            return new JObject {
                ["progress"] = _progress,
                ["resolvedCycles"] = _resolvedCycles,
                ["rngState"] = _random.State.ToString(CultureInfo.InvariantCulture),
                ["offers"] = offers,
                ["pending"] = pending,
                ["active"] = active
            };
        }

        public void Deserialize(JToken state) {
            RestoreData restore = ParseRestore(state);
            if (!_postInitialized) {
                _deferredRestore = restore;
                return;
            }
            ApplyRestore(restore);
        }

        private RestoreData ParseRestore(JToken state) {
            if (state is not JObject data) throw new JsonSerializationException("Research save data must be an object.");
            double progress = ReadFiniteNonNegative(data["progress"], "progress");
            long cycles = ReadNonNegativeLong(data["resolvedCycles"], "resolvedCycles");
            string rngText = data["rngState"]?.Type == JTokenType.String ? data["rngState"].Value<string>() : null;
            if (!ulong.TryParse(rngText, NumberStyles.None, CultureInfo.InvariantCulture, out ulong rngState)) {
                throw new JsonSerializationException("Research save data contains an invalid RNG state.");
            }

            if (data["offers"] is not JArray offerArray) throw new JsonSerializationException("Research save data is missing offers.");
            var offerIds = new List<string>(offerArray.Count);
            var offerSet = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < offerArray.Count; index++) {
                string id = ReadId(offerArray[index], $"offers[{index}]");
                if (!offerSet.Add(id)) throw new JsonSerializationException($"Research save contains duplicate offer '{id}'.");
                offerIds.Add(id);
            }

            PendingRestore pending = null;
            JToken pendingToken = data["pending"];
            if (pendingToken != null && pendingToken.Type != JTokenType.Null) {
                if (pendingToken is not JObject pendingObject) throw new JsonSerializationException("Research pending state must be an object or null.");
                string id = ReadId(pendingObject["practiceId"], "pending.practiceId");
                double threshold = ReadFiniteNonNegative(pendingObject["signatureThreshold"], "pending.signatureThreshold");
                if (threshold <= 0d || threshold > 1d) throw new JsonSerializationException("Research pending threshold must be in (0,1].");
                pending = new PendingRestore(id, (float)threshold);
            }
            if (pending != null && offerIds.Count > 0) {
                throw new JsonSerializationException("Research save cannot contain both offers and a pending practice.");
            }

            if (data["active"] is not JArray activeArray) throw new JsonSerializationException("Research save data is missing active practices.");
            var active = new List<ActiveRestore>(activeArray.Count);
            var activeSet = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < activeArray.Count; index++) {
                if (activeArray[index] is not JObject activeObject) throw new JsonSerializationException($"Research active[{index}] must be an object.");
                string id = ReadId(activeObject["practiceId"], $"active[{index}].practiceId");
                if (!activeSet.Add(id)) throw new JsonSerializationException($"Research save contains duplicate active practice '{id}'.");
                double effectiveness = ReadFiniteNonNegative(activeObject["effectiveness"], $"active[{index}].effectiveness");
                if (effectiveness > float.MaxValue) throw new JsonSerializationException("Research active effectiveness exceeds Single range.");
                if (activeObject["permanent"]?.Type != JTokenType.Boolean) throw new JsonSerializationException("Research active permanent flag is invalid.");
                bool permanent = activeObject["permanent"].Value<bool>();
                double remaining = 0d;
                if (!permanent) {
                    if (!effectiveness.Equals(1d)) {
                        throw new JsonSerializationException(
                            "Timed research practice must have effectiveness exactly 1.");
                    }
                    remaining = ReadFiniteNonNegative(activeObject["remainingSeconds"], $"active[{index}].remainingSeconds");
                    if (remaining <= 0d) throw new JsonSerializationException("Timed research practice must have positive remaining time.");
                }
                active.Add(new ActiveRestore(id, (float)effectiveness, permanent, remaining));
            }
            return new RestoreData(progress, cycles, rngState, offerIds, pending, active);
        }

        private void ApplyRestore(RestoreData restore) {
            if (restore == null) throw new ArgumentNullException(nameof(restore));
            var candidateActive = new List<ActivePracticeState>();
            var candidateActiveIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < restore.Active.Count; index++) {
                ActiveRestore saved = restore.Active[index];
                if (!_practices.TryGetValue(saved.PracticeId, out PracticeDefinition definition)) {
                    Debug.LogWarning($"Saved active practice '{saved.PracticeId}' no longer exists and was dropped.");
                    continue;
                }
                if (definition.EffectKind != PracticeEffectKind.NumericModifiers || !saved.Permanent && saved.RemainingSeconds <= 0d) {
                    Debug.LogWarning($"Saved active practice '{saved.PracticeId}' is incompatible with its current definition and was dropped.");
                    continue;
                }
                if (!candidateActiveIds.Add(saved.PracticeId)) throw new JsonSerializationException($"Duplicate active practice '{saved.PracticeId}'.");
                candidateActive.Add(new ActivePracticeState(definition, saved.Effectiveness, saved.Permanent, saved.RemainingSeconds));
            }

            PendingPracticeState? candidatePending = null;
            bool pendingWasUnknown = false;
            if (restore.Pending != null) {
                if (!_practices.TryGetValue(restore.Pending.PracticeId, out PracticeDefinition pendingDefinition)) {
                    Debug.LogWarning($"Saved pending practice '{restore.Pending.PracticeId}' no longer exists; the offer will be regenerated.");
                    pendingWasUnknown = true;
                }
                else {
                    if (pendingDefinition.EffectKind == PracticeEffectKind.NumericModifiers && candidateActiveIds.Contains(pendingDefinition.Id)) {
                        throw new JsonSerializationException("Research save contains the same modifier practice as pending and active.");
                    }
                    candidatePending = new PendingPracticeState(restore.Pending.PracticeId, restore.Pending.SignatureThreshold);
                }
            }

            var candidateOffers = new List<PracticeDefinition>();
            bool offersInvalid = false;
            int currentOfferLimit = Math.Clamp(_researchData.Value.OfferCount, 1, 64);
            if (restore.OfferIds.Count > currentOfferLimit) offersInvalid = true;
            for (int index = 0; index < restore.OfferIds.Count; index++) {
                string id = restore.OfferIds[index];
                if (!_practices.TryGetValue(id, out PracticeDefinition definition) ||
                    definition.EffectKind == PracticeEffectKind.NumericModifiers && candidateActiveIds.Contains(id)) {
                    offersInvalid = true;
                    continue;
                }
                candidateOffers.Add(definition);
            }
            if (candidateOffers.Count != restore.OfferIds.Count) offersInvalid = true;

            HashSet<Type> oldGroups = CollectAffectedGroups(_active);
            _isRestoring = true;
            try {
                _active.Clear();
                _active.AddRange(candidateActive);
                _offers.Clear();
                if (!offersInvalid) _offers.AddRange(candidateOffers);
                _pending = candidatePending;
                _resolvedCycles = restore.ResolvedCycles;
                _progress = restore.Progress;
                _random = new ResearchRandom(restore.RngState);
                InvalidateClaims();
                oldGroups.UnionWith(CollectAffectedGroups(_active));
                InvalidateGroups(oldGroups);
                double required = RequiredPoints;
                _progress = Math.Min(_progress, required);
                if ((offersInvalid || pendingWasUnknown) && !_pending.HasValue) {
                    _offers.Clear();
                    _progress = required;
                }
            }
            finally { _isRestoring = false; }
            ReconcileProgress();
            NotifyChanged();
            NotifyDocumentOffersChanged();
        }

        private void ResetRuntimeState() {
            HashSet<Type> oldGroups = CollectAffectedGroups(_active);
            _offers.Clear();
            _active.Clear();
            _pending = null;
            _progress = 0d;
            _resolvedCycles = 0L;
            InvalidateClaims();
            InvalidateGroups(oldGroups);
            NotifyChanged();
            NotifyDocumentOffersChanged();
        }

        private static HashSet<Type> CollectAffectedGroups(IReadOnlyList<ActivePracticeState> active) {
            var groups = new HashSet<Type>();
            if (active == null) return groups;
            for (int activeIndex = 0; activeIndex < active.Count; activeIndex++) {
                ModifierDefinition[] definitions = active[activeIndex].Definition.Modifiers;
                if (definitions == null) continue;
                for (int definitionIndex = 0; definitionIndex < definitions.Length; definitionIndex++) {
                    if (definitions[definitionIndex]?.NumericModifiers == null) continue;
                    for (int modifierIndex = 0; modifierIndex < definitions[definitionIndex].NumericModifiers.Count; modifierIndex++) {
                        NumericModifierDefinition modifier = definitions[definitionIndex].NumericModifiers[modifierIndex];
                        if (modifier != null) groups.Add(modifier.GetGroupType());
                    }
                }
            }
            return groups;
        }

        private void InvalidateGroups(IEnumerable<Type> groups) {
            if (_cacheInvalidator == null || groups == null) return;
            foreach (Type group in groups) _cacheInvalidator.Invalidate(group);
        }

        private static string ReadId(JToken token, string path) {
            string value = token?.Type == JTokenType.String ? token.Value<string>() : null;
            if (string.IsNullOrWhiteSpace(value)) throw new JsonSerializationException($"Research save field '{path}' requires a non-empty string.");
            return value;
        }

        private static double ReadFiniteNonNegative(JToken token, string path) {
            if (token?.Type is not (JTokenType.Integer or JTokenType.Float)) throw new JsonSerializationException($"Research save field '{path}' requires a number.");
            double value = token.Value<double>();
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d) throw new JsonSerializationException($"Research save field '{path}' is invalid.");
            return value;
        }

        private static long ReadNonNegativeLong(JToken token, string path) {
            if (token?.Type != JTokenType.Integer) throw new JsonSerializationException($"Research save field '{path}' requires an integer.");
            long value = token.Value<long>();
            if (value < 0L) throw new JsonSerializationException($"Research save field '{path}' cannot be negative.");
            return value;
        }

        private sealed class RestoreData {
            public double Progress { get; }
            public long ResolvedCycles { get; }
            public ulong RngState { get; }
            public IReadOnlyList<string> OfferIds { get; }
            public PendingRestore Pending { get; }
            public IReadOnlyList<ActiveRestore> Active { get; }

            public RestoreData(double progress, long resolvedCycles, ulong rngState,
                IReadOnlyList<string> offerIds, PendingRestore pending, IReadOnlyList<ActiveRestore> active) {
                Progress = progress;
                ResolvedCycles = resolvedCycles;
                RngState = rngState;
                OfferIds = offerIds;
                Pending = pending;
                Active = active;
            }
        }

        private sealed class PendingRestore {
            public string PracticeId { get; }
            public float SignatureThreshold { get; }
            public PendingRestore(string practiceId, float signatureThreshold) {
                PracticeId = practiceId;
                SignatureThreshold = signatureThreshold;
            }
        }

        private readonly struct ActiveRestore {
            public string PracticeId { get; }
            public float Effectiveness { get; }
            public bool Permanent { get; }
            public double RemainingSeconds { get; }
            public ActiveRestore(string practiceId, float effectiveness, bool permanent, double remainingSeconds) {
                PracticeId = practiceId;
                Effectiveness = effectiveness;
                Permanent = permanent;
                RemainingSeconds = remainingSeconds;
            }
        }
    }
}
