using System;
using Constants;
using Data.Statistics;
using NUnit.Framework;
using R3;
using Services;
using UnityEngine;

namespace Tests.EditMode {
    public sealed class StatisticsTabViewModelTests {
        private readonly System.Collections.Generic.List<UnityEngine.Object> _objects = new();

        [TearDown]
        public void TearDown() {
            for (int index = _objects.Count - 1; index >= 0; index--) {
                if (_objects[index] != null) UnityEngine.Object.DestroyImmediate(_objects[index]);
            }

            _objects.Clear();
        }

        [Test]
        public void Constructor_BuildsCategoriesFromLayout() {
            StatisticsTabLayoutDefinition layout = CreateLayout(
                new StatisticsTabCategory("Economy", new() {
                    new StatisticsTabTracker(GameStatisticIds.MoneyTotalEarned, "Total earned"),
                    new StatisticsTabTracker(GameStatisticIds.MoneyMaxBalance, "Max balance")
                }),
                new StatisticsTabCategory("Office", new() {
                    new StatisticsTabTracker(GameStatisticIds.OfficeClerkCount, "Clerks")
                }));
            var statistics = new GameStatisticsService();
            statistics.SetValue(GameStatisticIds.MoneyTotalEarned,
                GameStatisticFormats.EncodeMoney(new Utils.Value(1000d)));

            using var viewModel = new Presentation.StatisticsTabViewModel(layout, statistics);

            Assert.That(viewModel.Categories.Count, Is.EqualTo(2));
            Assert.That(viewModel.Categories[0].Title, Is.EqualTo("Economy"));
            Assert.That(viewModel.Categories[0].Rows.Count, Is.EqualTo(2));
            Assert.That(viewModel.Categories[0].Rows[0].Label, Is.EqualTo("Total earned"));
            Assert.That(viewModel.Categories[0].Rows[0].StatisticId, Is.EqualTo(GameStatisticIds.MoneyTotalEarned));
            Assert.That(viewModel.Categories[0].Rows[0].Value, Is.EqualTo("1k"));
            Assert.That(viewModel.Categories[0].Rows[1].Value, Is.EqualTo("0"));
            Assert.That(viewModel.Categories[1].Title, Is.EqualTo("Office"));
            Assert.That(viewModel.Categories[1].Rows[0].Label, Is.EqualTo("Clerks"));
        }

        [Test]
        public void Constructor_ThrowsOnNullArguments() {
            var statistics = new GameStatisticsService();
            Assert.That(() => new Presentation.StatisticsTabViewModel(null, statistics),
                Throws.ArgumentNullException);
            Assert.That(() => new Presentation.StatisticsTabViewModel(CreateLayout(), null),
                Throws.ArgumentNullException);
        }

        [Test]
        public void Refresh_FormatsValuesPerStatisticKind() {
            StatisticsTabLayoutDefinition layout = CreateLayout(
                new StatisticsTabCategory("All", new() {
                    new StatisticsTabTracker(GameStatisticIds.CritSignatureChance, "Signature crit chance"),
                    new StatisticsTabTracker(GameStatisticIds.CritSignatureMultiplier, "Signature crit multiplier"),
                    new StatisticsTabTracker(GameStatisticIds.MultiPaySignatureChance, "Signature multi-pay"),
                    new StatisticsTabTracker(GameStatisticIds.IncomeClerkIncome, "Clerk income"),
                    new StatisticsTabTracker(GameStatisticIds.DocumentsConsumedPerSecond, "Consumed/s"),
                    new StatisticsTabTracker(GameStatisticIds.MoneyIncomePerSecond, "Income/s"),
                    new StatisticsTabTracker(GameStatisticIds.BillsAcceptedCount, "Bills accepted"),
                    new StatisticsTabTracker(GameStatisticIds.OfficeClerkCount, "Clerks"),
                    new StatisticsTabTracker("custom.unknown_stat", "Unknown")
                }));
            var statistics = new GameStatisticsService();
            statistics.SetValue(GameStatisticIds.CritSignatureChance, 0.156d);
            statistics.SetValue(GameStatisticIds.CritSignatureMultiplier, 2.5d);
            statistics.SetValue(GameStatisticIds.MultiPaySignatureChance, 12.5d);
            statistics.SetValue(GameStatisticIds.IncomeClerkIncome, 0.75d);
            statistics.SetValue(GameStatisticIds.DocumentsConsumedPerSecond, 2.5d);
            statistics.SetValue(GameStatisticIds.MoneyIncomePerSecond,
                GameStatisticFormats.EncodeMoney(new Utils.Value(1500d)));
            statistics.SetValue(GameStatisticIds.BillsAcceptedCount, 1234d);
            statistics.SetValue(GameStatisticIds.OfficeClerkCount, 7d);

            using var viewModel = new Presentation.StatisticsTabViewModel(layout, statistics);
            bool changed = viewModel.Refresh();

            Assert.That(changed, Is.False, "Values are already formatted at construction");
            string[] values = ReadRowValues(viewModel);
            Assert.That(values[0], Is.EqualTo("15.6%"));
            Assert.That(values[1], Is.EqualTo("x2.5"));
            Assert.That(values[2], Is.EqualTo("12.5%"));
            Assert.That(values[3], Is.EqualTo("x0.75"));
            Assert.That(values[4], Is.EqualTo("2.5/s"));
            Assert.That(values[5], Is.EqualTo("1.5k/s"));
            Assert.That(values[6], Is.EqualTo("1,234"));
            Assert.That(values[7], Is.EqualTo("7"));
            Assert.That(values[8], Is.EqualTo("0"), "Unknown statistic falls back to zero");
        }

        [Test]
        public void Refresh_ReturnsTrueOnlyWhenValuesChange() {
            StatisticsTabLayoutDefinition layout = CreateLayout(
                new StatisticsTabCategory("Economy", new() {
                    new StatisticsTabTracker(GameStatisticIds.MoneyTotalEarned, "Total earned")
                }));
            var statistics = new GameStatisticsService();
            statistics.SetValue(GameStatisticIds.MoneyTotalEarned,
                GameStatisticFormats.EncodeMoney(new Utils.Value(100d)));

            using var viewModel = new Presentation.StatisticsTabViewModel(layout, statistics);
            Assert.That(viewModel.Refresh(), Is.False);
            Assert.That(viewModel.Categories[0].Rows[0].Value, Is.EqualTo("100"));

            statistics.SetValue(GameStatisticIds.MoneyTotalEarned,
                GameStatisticFormats.EncodeMoney(new Utils.Value(1000d)));
            Assert.That(viewModel.Refresh(), Is.True);
            Assert.That(viewModel.Categories[0].Rows[0].Value, Is.EqualTo("1k"));
            Assert.That(viewModel.Refresh(), Is.False);
        }

        [Test]
        public void Changed_FiresWhenStatisticsChange() {
            StatisticsTabLayoutDefinition layout = CreateLayout(
                new StatisticsTabCategory("Economy", new() {
                    new StatisticsTabTracker(GameStatisticIds.MoneyTotalEarned, "Total earned")
                }));
            var statistics = new GameStatisticsService();
            using var viewModel = new Presentation.StatisticsTabViewModel(layout, statistics);

            int fired = 0;
            using IDisposable subscription = viewModel.Changed.Subscribe(_ => fired++);
            statistics.SetValue(GameStatisticIds.MoneyTotalEarned, 2d);

            Assert.That(fired, Is.EqualTo(1));
        }

        [TestCase(0d, "0")]
        [TestCase(0.1d, "0.1")]
        [TestCase(1d, "1")]
        [TestCase(1000d, "1k")]
        public void MoneyEncoding_RoundTripsAndFormats(double amount, string expectedText) {
            var value = new Utils.Value(amount);
            double encoded = GameStatisticFormats.EncodeMoney(value);

            Assert.That(GameStatisticFormats.TryDecodeMoney(encoded, out Utils.Value decoded), Is.True);
            if (value.IsZero) Assert.That(decoded.IsZero, Is.True);
            else Assert.That(decoded.ToLog10(), Is.EqualTo(value.ToLog10()).Within(0.000001d));
            Assert.That(GameStatisticFormats.Format(encoded, GameStatisticFormat.EncodedMoney),
                Is.EqualTo(expectedText));
        }

        [Test]
        public void Constructor_SkipsMalformedSerializedEntries() {
            StatisticsTabLayoutDefinition layout = CreateLayout(
                null,
                new StatisticsTabCategory("Broken", null),
                new StatisticsTabCategory("Valid", new() {
                    null,
                    new StatisticsTabTracker(string.Empty, "Missing id"),
                    new StatisticsTabTracker(GameStatisticIds.OfficeClerkCount, "Clerks")
                }));

            using var viewModel = new Presentation.StatisticsTabViewModel(layout, new GameStatisticsService());

            Assert.That(viewModel.Categories.Count, Is.EqualTo(1));
            Assert.That(viewModel.Categories[0].Title, Is.EqualTo("Valid"));
            Assert.That(viewModel.Categories[0].Rows.Count, Is.EqualTo(1));
        }

        private static string[] ReadRowValues(Presentation.StatisticsTabViewModel viewModel) {
            var rows = viewModel.Categories[0].Rows;
            var values = new string[rows.Count];
            for (int index = 0; index < rows.Count; index++) values[index] = rows[index].Value;
            return values;
        }

        private StatisticsTabLayoutDefinition CreateLayout(params StatisticsTabCategory[] categories) {
            StatisticsTabLayoutDefinition layout = ScriptableObject.CreateInstance<StatisticsTabLayoutDefinition>();
            layout.Initialize(new System.Collections.Generic.List<StatisticsTabCategory>(categories));
            _objects.Add(layout);
            return layout;
        }
    }
}
