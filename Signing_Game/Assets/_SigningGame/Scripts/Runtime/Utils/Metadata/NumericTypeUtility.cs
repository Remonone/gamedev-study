using System;
using System.Collections.Generic;

namespace Utils.Metadata {
    public static class NumericTypeUtility {
        private static readonly HashSet<Type> SupportedTypes = new() {
            typeof(byte),
            typeof(sbyte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(float),
            typeof(double),
            typeof(decimal),
            typeof(Value)
        };
        
        public static bool IsSupportedType(Type type) => SupportedTypes.Contains(type);

        public static void GetFiniteRange(Type type, out double minimum, out double maximum) {
            if (type == typeof(byte)) { minimum = byte.MinValue; maximum = byte.MaxValue; return; }
            if (type == typeof(sbyte)) { minimum = sbyte.MinValue; maximum = sbyte.MaxValue; return; }
            if (type == typeof(short)) { minimum = short.MinValue; maximum = short.MaxValue; return; }
            if (type == typeof(ushort)) { minimum = ushort.MinValue; maximum = ushort.MaxValue; return; }
            if (type == typeof(int)) { minimum = int.MinValue; maximum = int.MaxValue; return; }
            if (type == typeof(uint)) { minimum = uint.MinValue; maximum = uint.MaxValue; return; }
            if (type == typeof(long)) { minimum = long.MinValue; maximum = long.MaxValue; return; }
            if (type == typeof(ulong)) { minimum = ulong.MinValue; maximum = ulong.MaxValue; return; }
            if (type == typeof(float)) { minimum = -float.MaxValue; maximum = float.MaxValue; return; }
            if (type == typeof(double)) { minimum = -double.MaxValue; maximum = double.MaxValue; return; }
            if (type == typeof(decimal)) {
                minimum = (double)decimal.MinValue;
                maximum = (double)decimal.MaxValue;
                return;
            }
            if (type == typeof(Value)) { minimum = 0d; maximum = double.MaxValue; return; }
            throw new NotSupportedException($"Unsupported type: {type}");
        }

        public static double ToDouble(object value) {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (value is Value v) {
                return v.ToDouble();
            }
            return Convert.ToDouble(value);
        }

        public static object FromDouble(double value, Type targetType) {
            if (targetType == typeof(byte)) {
                if (value <= byte.MinValue) return byte.MinValue;
                if (value >= byte.MaxValue) return byte.MaxValue;
                return checked((byte)Math.Round(value));
            }
            if (targetType == typeof(sbyte)) {
                if (value <= sbyte.MinValue) return sbyte.MinValue;
                if (value >= sbyte.MaxValue) return sbyte.MaxValue;
                return checked((sbyte)Math.Round(value));
            }

            if (targetType == typeof(short)) {
                if (value <= short.MinValue) return short.MinValue;
                if (value >= short.MaxValue) return short.MaxValue;
                return checked((short)Math.Round(value));
            }

            if (targetType == typeof(ushort)) {
                if (value <= ushort.MinValue) return ushort.MinValue;
                if (value >= ushort.MaxValue) return ushort.MaxValue;
                return checked((ushort)Math.Round(value));
            }

            if (targetType == typeof(int)) {
                if (value <= int.MinValue) return int.MinValue;
                if (value >= int.MaxValue) return int.MaxValue;
                return checked((int)Math.Round(value));
            }

            if (targetType == typeof(uint)) {
                if (value <= uint.MinValue) return uint.MinValue;
                if (value >= uint.MaxValue) return uint.MaxValue;
                return checked((uint)Math.Round(value));
            }

            if (targetType == typeof(long)) {
                if (value <= long.MinValue) return long.MinValue;
                if (value >= long.MaxValue) return long.MaxValue;
                return checked((long)Math.Round(value));
            }

            if (targetType == typeof(ulong)) {
                if (value <= ulong.MinValue) return ulong.MinValue;
                if (value >= ulong.MaxValue) return ulong.MaxValue;
                return checked((ulong)Math.Round(value));
            }

            if (targetType == typeof(float)) {
                return (float)value;
            }

            if (targetType == typeof(double)) {
                return value;
            }
            if (targetType == typeof(decimal)) {
                if (value <= (double)decimal.MinValue) return decimal.MinValue;
                if (value >= (double)decimal.MaxValue) return decimal.MaxValue;
                return (decimal)value;
            }

            if (targetType == typeof(Value)) {
                return new Value(value);
            }
            throw new NotSupportedException($"Unsupported type: {targetType}");
        }
    }
}
