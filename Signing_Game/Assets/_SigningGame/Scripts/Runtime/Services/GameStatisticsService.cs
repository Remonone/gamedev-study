using System;
using System.Collections.Generic;
using Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using R3;

namespace Services {
    public enum GameStatisticMutationKind {
        Set = 0,
        Add = 1
    }

    public readonly struct GameStatisticMutation {
        public string StatisticId { get; }
        public GameStatisticMutationKind Kind { get; }
        public double Value { get; }

        private GameStatisticMutation(string statisticId, GameStatisticMutationKind kind, double value) {
            StatisticId = statisticId;
            Kind = kind;
            Value = value;
        }

        public static GameStatisticMutation Set(string statisticId, double value) {
            return new GameStatisticMutation(statisticId, GameStatisticMutationKind.Set, value);
        }

        public static GameStatisticMutation Add(string statisticId, double value) {
            return new GameStatisticMutation(statisticId, GameStatisticMutationKind.Add, value);
        }
    }

    public sealed class GameStatisticsService : IService, ISaveable {
        private readonly Dictionary<string, double> _values = new(StringComparer.Ordinal);
        private readonly Subject<Unit> _changed = new();

        public string SaveId => "GameStatistics";
        public Observable<Unit> Changed => _changed;

        public bool SetValue(string statisticId, double value) {
            ValidateEntry(statisticId, value);
            if (_values.TryGetValue(statisticId, out double current) && current.Equals(value)) return false;

            _values[statisticId] = value;
            _changed.OnNext(Unit.Default);
            return true;
        }

        public double AddValue(string statisticId, double amount) {
            ValidateEntry(statisticId, amount);
            _values.TryGetValue(statisticId, out double current);
            double next = current + amount;
            ValidateEntry(statisticId, next);
            SetValue(statisticId, next);
            return next;
        }

        public bool ApplyBatch(IReadOnlyList<GameStatisticMutation> mutations, int count) {
            if (mutations == null) throw new ArgumentNullException(nameof(mutations));
            if (count < 0 || count > mutations.Count) throw new ArgumentOutOfRangeException(nameof(count));

            for (int index = 0; index < count; index++) {
                GameStatisticMutation mutation = mutations[index];
                ValidateEntry(mutation.StatisticId, mutation.Value);
                if (!Enum.IsDefined(typeof(GameStatisticMutationKind), mutation.Kind)) {
                    throw new ArgumentOutOfRangeException(nameof(mutations),
                        $"Statistic mutation at index {index} has an invalid kind.");
                }

                for (int previous = 0; previous < index; previous++) {
                    if (string.Equals(mutations[previous].StatisticId, mutation.StatisticId,
                            StringComparison.Ordinal)) {
                        throw new ArgumentException(
                            $"Statistic '{mutation.StatisticId}' occurs more than once in the batch.",
                            nameof(mutations));
                    }
                }

                _values.TryGetValue(mutation.StatisticId, out double current);
                double next = mutation.Kind == GameStatisticMutationKind.Add
                    ? current + mutation.Value
                    : mutation.Value;
                ValidateEntry(mutation.StatisticId, next);
            }

            bool changed = false;
            for (int index = 0; index < count; index++) {
                GameStatisticMutation mutation = mutations[index];
                _values.TryGetValue(mutation.StatisticId, out double current);
                double next = mutation.Kind == GameStatisticMutationKind.Add
                    ? current + mutation.Value
                    : mutation.Value;
                if (_values.ContainsKey(mutation.StatisticId) && current.Equals(next)) continue;
                _values[mutation.StatisticId] = next;
                changed = true;
            }

            if (changed) _changed.OnNext(Unit.Default);
            return changed;
        }

        public bool TryGetValue(string statisticId, out double value) {
            if (string.IsNullOrWhiteSpace(statisticId)) {
                value = default;
                return false;
            }

            return _values.TryGetValue(statisticId, out value);
        }

        public JToken Serialize() {
            var result = new JObject();
            var keys = new List<string>(_values.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string key in keys) result.Add(key, _values[key]);
            return result;
        }

        public void Deserialize(JToken state) {
            if (state is not JObject data) {
                throw new JsonSerializationException("Game statistics state must be an object.");
            }

            var restored = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (JProperty property in data.Properties()) {
                if (property.Value.Type is not (JTokenType.Integer or JTokenType.Float)) {
                    throw new JsonSerializationException(
                        $"Statistic '{property.Name}' must contain a numeric value.");
                }

                double value = property.Value.Value<double>();
                try {
                    ValidateEntry(property.Name, value);
                } catch (ArgumentException exception) {
                    throw new JsonSerializationException(exception.Message, exception);
                }

                if (!restored.TryAdd(property.Name, value)) {
                    throw new JsonSerializationException($"Duplicate statistic key '{property.Name}'.");
                }
            }

            if (DictionaryEquals(restored)) return;

            _values.Clear();
            foreach (KeyValuePair<string, double> pair in restored) _values.Add(pair.Key, pair.Value);
            _changed.OnNext(Unit.Default);
        }

        public void Dispose() {
            _changed.Dispose();
            _values.Clear();
        }

        private bool DictionaryEquals(Dictionary<string, double> other) {
            if (_values.Count != other.Count) return false;
            foreach (KeyValuePair<string, double> pair in _values) {
                if (!other.TryGetValue(pair.Key, out double value) || !value.Equals(pair.Value)) return false;
            }

            return true;
        }

        private static void ValidateEntry(string statisticId, double value) {
            if (string.IsNullOrWhiteSpace(statisticId)) {
                throw new ArgumentException("Statistic ID cannot be empty.", nameof(statisticId));
            }

            if (double.IsNaN(value) || double.IsInfinity(value)) {
                throw new ArgumentException("Statistic value must be finite.", nameof(value));
            }
        }
    }
}
