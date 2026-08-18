using System;
using System.Collections.Generic;
using Utils;

namespace Constants {
    public enum GameStatisticFormat {
        Default = 0,
        Integer = 1,
        PercentUnit = 2,
        PercentHundred = 3,
        Multiplier = 4,
        EncodedMoney = 5,
        PerSecond = 6,
        EncodedMoneyPerSecond = 7
    }

    /// <summary>
    /// Resolves how a statistic id is rendered in the statistics tab.
    /// The designer-facing tracker config only carries (statistic id, display name),
    /// so formatting is derived from the id here.
    /// </summary>
    public static class GameStatisticFormats {
        private static readonly Dictionary<string, GameStatisticFormat> Formats = new(StringComparer.Ordinal) {
            [GameStatisticIds.OfficeClerkCount] = GameStatisticFormat.Integer,
            [GameStatisticIds.OfficeProcessedDocuments] = GameStatisticFormat.Integer,
            [GameStatisticIds.OfficeAcceptedDocuments] = GameStatisticFormat.Integer,
            [GameStatisticIds.OfficeRejectedDocuments] = GameStatisticFormat.Integer,
            [GameStatisticIds.OfficeClerkCapacity] = GameStatisticFormat.Integer,
            [GameStatisticIds.MoneyIncomePerSecond] = GameStatisticFormat.EncodedMoneyPerSecond,
            [GameStatisticIds.MoneyTotalEarned] = GameStatisticFormat.EncodedMoney,
            [GameStatisticIds.MoneyTotalSpent] = GameStatisticFormat.EncodedMoney,
            [GameStatisticIds.MoneyMaxBalance] = GameStatisticFormat.EncodedMoney,
            [GameStatisticIds.DocumentsGeneratedPerSecond] = GameStatisticFormat.PerSecond,
            [GameStatisticIds.DocumentsProcessedPerSecond] = GameStatisticFormat.PerSecond,
            [GameStatisticIds.DocumentsConsumedPerSecond] = GameStatisticFormat.PerSecond,
            [GameStatisticIds.DocumentsGeneratedTotal] = GameStatisticFormat.Integer,
            [GameStatisticIds.DocumentsConsumedTotal] = GameStatisticFormat.Integer,
            [GameStatisticIds.DocumentsSuccessfullySigned] = GameStatisticFormat.Integer,
            [GameStatisticIds.BillsAcceptedCount] = GameStatisticFormat.Integer,
            [GameStatisticIds.IncomeSignatureIncome] = GameStatisticFormat.EncodedMoney,
            [GameStatisticIds.IncomeClerkIncome] = GameStatisticFormat.Multiplier,
            [GameStatisticIds.IncomeBankIncome] = GameStatisticFormat.EncodedMoney,
            [GameStatisticIds.CritSignatureChance] = GameStatisticFormat.PercentUnit,
            [GameStatisticIds.CritSignatureMultiplier] = GameStatisticFormat.Multiplier,
            [GameStatisticIds.CritClerkChance] = GameStatisticFormat.PercentUnit,
            [GameStatisticIds.CritClerkMultiplier] = GameStatisticFormat.Multiplier,
            [GameStatisticIds.CritBankChance] = GameStatisticFormat.PercentUnit,
            [GameStatisticIds.CritBankMultiplier] = GameStatisticFormat.Multiplier,
            [GameStatisticIds.MultiPaySignatureChance] = GameStatisticFormat.PercentHundred,
            [GameStatisticIds.MultiPayClerkChance] = GameStatisticFormat.PercentHundred,
            [GameStatisticIds.MultiPayBankChance] = GameStatisticFormat.PercentHundred
        };

        public static GameStatisticFormat Resolve(string statisticId) {
            return statisticId != null && Formats.TryGetValue(statisticId, out GameStatisticFormat format)
                ? format
                : GameStatisticFormat.Default;
        }

        /// <summary>
        /// Formats a raw statistic value for display. Missing statistics (not yet tracked)
        /// render as "0" regardless of format kind.
        /// </summary>
        public static string Format(double value, GameStatisticFormat format) {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "0";

            switch (format) {
                case GameStatisticFormat.Integer:
                    return value <= 0d
                        ? "0"
                        : Math.Floor(value).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
                case GameStatisticFormat.PercentUnit:
                    return ToPercent(value * 100d);
                case GameStatisticFormat.PercentHundred:
                    return ToPercent(value);
                case GameStatisticFormat.Multiplier:
                    return "x" + value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                case GameStatisticFormat.EncodedMoney:
                    return FormatEncodedMoney(value);
                case GameStatisticFormat.PerSecond:
                    return value <= 0d
                        ? "0/s"
                        : value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "/s";
                case GameStatisticFormat.EncodedMoneyPerSecond:
                    return FormatEncodedMoney(value) + "/s";
                default:
                    return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private static string ToPercent(double percent) {
            if (percent <= 0d) return "0%";
            return percent.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "%";
        }

        public static double EncodeMoney(Value value) {
            if (value.IsZero) return 0d;

            double log10 = value.ToLog10();
            return log10 >= 0d ? log10 + 1d : 1d / (1d - log10);
        }

        public static bool TryDecodeMoney(double encoded, out Value value) {
            value = Value.Zero;
            if (double.IsNaN(encoded) || double.IsInfinity(encoded) || encoded <= 0d) return encoded == 0d;

            double log10 = encoded >= 1d ? encoded - 1d : 1d - 1d / encoded;
            value = Value.FromLog10(log10);
            return true;
        }

        private static string FormatEncodedMoney(double encoded) {
            return TryDecodeMoney(encoded, out Value value) ? value.ToString() : "0";
        }
    }
}
