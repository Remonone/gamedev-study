using System;
using System.Collections.Generic;

namespace Utils.Metadata {
    public class NumericTypeUtility {
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
            typeof(decimal)
        };
        
        public static bool IsSupportedType(Type type) => SupportedTypes.Contains(type);

        public static double ToDouble(object value) {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            return Convert.ToDouble(value);
        }

        public static object FromDouble(double value, Type targetType) {
            if (targetType == typeof(byte)) {
                return checked((byte)Math.Round(value));
            }
            if (targetType == typeof(sbyte)) {
                return checked((sbyte)Math.Round(value));
            }

            if (targetType == typeof(short)) {
                return checked((short)Math.Round(value));
            }

            if (targetType == typeof(ushort)) {
                return checked((ushort)Math.Round(value));
            }

            if (targetType == typeof(int)) {
                return checked((int)Math.Round(value));
            }

            if (targetType == typeof(uint)) {
                return checked((uint)Math.Round(value));
            }

            if (targetType == typeof(long)) {
                return checked((long)Math.Round(value));
            }

            if (targetType == typeof(ulong)) {
                return checked((ulong)Math.Round(value));
            }

            if (targetType == typeof(float)) {
                return (float)Math.Round(value);
            }

            if (targetType == typeof(double)) {
                return value;
            }
            if (targetType == typeof(decimal)) {
                return (decimal)value;
            }
            throw new NotSupportedException($"Unsupported type: {targetType}");
        }
    }
}