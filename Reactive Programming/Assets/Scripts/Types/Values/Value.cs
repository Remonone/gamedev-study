using System;
using System.Globalization;
using Newtonsoft.Json;
using UnityEngine;

namespace Types.Enums.Values {
    [Serializable]
    public struct Value : IComparable<Value>, IEquatable<Value> {
        private const double BaseStep = 1000d;
        private const double Log10BaseStep = 3d;

        public static readonly Value Zero = new(0d);
        public static readonly Value One = new(1d);

        [SerializeField, JsonProperty("stored"), Tooltip("Mantissa stored in the current thousand-based degree.")]
        private double _stored;

        [SerializeField, JsonProperty("base"), Tooltip("Thousand-based magnitude bucket used with Stored.")]
        private Base _base;

        public double Stored => _stored;
        public Base Base => _base;
        public bool IsZero => _stored <= 0d;

        public Value(double stored, Base baseValue) {
            _stored = stored;
            _base = baseValue;
            Normalize();
        }

        public Value(double value) {
            if (double.IsNaN(value) || double.IsInfinity(value)) {
                throw new ArgumentException("Value must be finite", nameof(value));
            }

            if (value < 0d) {
                throw new ArgumentException("Value cannot be negative");
            }

            if (value <= 0d) {
                _stored = 0d;
                _base = new Base { Degree = 0 };
                return;
            }

            if (value < BaseStep) {
                _stored = value;
                _base = new Base { Degree = 0 };
                return;
            }

            var degree = (int)Math.Floor(Math.Log10(value) / Log10BaseStep);
            _stored = value / Math.Pow(BaseStep, degree);
            _base = new Base { Degree = degree };
            Normalize();
        }

        public static Value FromLog10(double log10Value) {
            if (double.IsNaN(log10Value)) {
                throw new ArgumentException("Value must be a number", nameof(log10Value));
            }

            if (double.IsNegativeInfinity(log10Value)) {
                return Zero;
            }

            if (double.IsPositiveInfinity(log10Value)) {
                throw new ArgumentException("Value is too large", nameof(log10Value));
            }

            if (log10Value < 0d) {
                return new Value(Math.Pow(10d, log10Value));
            }

            var degree = (int)Math.Floor(log10Value / Log10BaseStep);
            var stored = Math.Pow(10d, log10Value - degree * Log10BaseStep);
            return new Value(stored, new Base { Degree = degree });
        }

        public static Value operator +(Value first, Value other) {
            var diff = first._base.Degree - other._base.Degree;
            // If the difference is more than a billion times, then ignore one of the values
            switch (diff) {
                case >= 3:
                    return first;
                case <= -3:
                    return other;
            }

            var degree = Math.Max(first._base.Degree, other._base.Degree);
            var newStored = first.ScaleToDegree(degree) + other.ScaleToDegree(degree);
            return new Value(newStored, new Base { Degree = degree });
        }

        public static Value? operator -(Value first, Value other) {
            if (other > first) {
                return null;
            }
            var diff = first._base.Degree - other._base.Degree;
            // If the difference is more than a billion times, then ignore one of the values
            if (diff > 3) {
                return first;
            }
            var newStored = first._stored - other.ScaleToDegree(first._base.Degree);
            var degree = first._base.Degree;
            return new Value(newStored, new Base { Degree = degree });
        }

        public static Value operator *(Value value, double multiplier) {
            if (multiplier < 0d) {
                throw new ArgumentException("Multiplier cannot be negative", nameof(multiplier));
            }

            if (value.IsZero || multiplier <= 0d) {
                return Zero;
            }

            return FromLog10(value.Log10Value() + Math.Log10(multiplier));
        }

        public static Value operator *(double multiplier, Value value) {
            return value * multiplier;
        }

        public static Value operator *(Value first, Value other) {
            if (first.IsZero || other.IsZero) {
                return Zero;
            }

            return FromLog10(first.Log10Value() + other.Log10Value());
        }

        public static Value operator /(Value value, double divisor) {
            if (divisor <= 0d) {
                throw new ArgumentException("Divisor must be positive", nameof(divisor));
            }

            if (value.IsZero) {
                return Zero;
            }

            return FromLog10(value.Log10Value() - Math.Log10(divisor));
        }

        public static bool operator >(Value first, Value other) {
            return first.CompareTo(other) > 0;
        }

        public static bool operator <(Value first, Value other) {
            return first.CompareTo(other) < 0;
        }

        public static bool operator >=(Value first, Value other) {
            return first.CompareTo(other) >= 0;
        }

        public static bool operator <=(Value first, Value other) {
            return first.CompareTo(other) <= 0;
        }

        public static bool operator ==(Value first, Value other) {
            return first.Equals(other);
        }

        public static bool operator !=(Value first, Value other) {
            return !first.Equals(other);
        }

        public int CompareTo(Value other) {
            if (_base.Degree == other._base.Degree) {
                return _stored.CompareTo(other._stored);
            }

            return _base.Degree.CompareTo(other._base.Degree);
        }

        public bool Equals(Value other) {
            return _base.Degree == other._base.Degree && _stored.Equals(other._stored);
        }

        public override bool Equals(object obj) {
            return obj is Value other && Equals(other);
        }

        public override int GetHashCode() {
            unchecked {
                return (_stored.GetHashCode() * 397) ^ _base.Degree;
            }
        }

        public double ToDouble() {
            if (IsZero) return 0d;

            var log = Log10Value();
            if (log > Math.Log10(double.MaxValue)) {
                return double.PositiveInfinity;
            }

            return _stored * Math.Pow(BaseStep, _base.Degree);
        }

        public float ToSingle() {
            var value = ToDouble();
            if (double.IsPositiveInfinity(value) || value > float.MaxValue) {
                return float.MaxValue;
            }

            return (float)value;
        }

        public Value Ceiling() {
            if (IsZero || _base.Degree > 0) {
                return this;
            }

            return new Value(Math.Ceiling(_stored));
        }

        private double ScaleToDegree(int targetDegree) {
            return _stored * Math.Pow(BaseStep, _base.Degree - targetDegree);
        }

        private double Log10Value() {
            return _base.Degree * Log10BaseStep + Math.Log10(_stored);
        }

        private void Normalize() {
            if (_stored <= 0d) {
                _stored = 0d;
                _base = new Base { Degree = 0 };
                return;
            }

            if (_stored >= BaseStep) {
                var shift = (int)Math.Floor(Math.Log10(_stored) / Log10BaseStep);
                _stored /= Math.Pow(BaseStep, shift);
                _base.Degree += shift;
            }

            while (_stored < 1d && _base.Degree > 0) {
                _stored *= BaseStep;
                _base.Degree--;
            }
        }

        public override string ToString() {
            return $"{_stored.ToString("0.##", CultureInfo.InvariantCulture)}{_base}";
        }
    }
}
