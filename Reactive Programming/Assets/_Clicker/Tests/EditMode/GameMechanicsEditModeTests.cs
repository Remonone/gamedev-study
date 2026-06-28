using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Economy;
using Economy.Providers;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using R3;
using Services;
using Services.Achievements;
using Services.Player;
using Services.Statistics;
using Types;
using Types.Achievements;
using Types.Buildings;
using Types.Enums;
using Types.Modifiers;
using Types.Modifiers.Cost;
using Types.Modifiers.Cost.Condition;
using Types.Modifiers.Cost.Formula;
using Types.Values;
using UnityEngine;
using Utils;

public sealed class GameMechanicsEditModeTests {
    private readonly List<UnityEngine.Object> _createdObjects = new();
    private readonly List<IDisposable> _disposables = new();

    [TearDown]
    public void TearDown() {
        foreach (var disposable in _disposables) disposable.Dispose();
        _disposables.Clear();

        foreach (var unityObject in _createdObjects) {
            if (unityObject != null) UnityEngine.Object.DestroyImmediate(unityObject);
        }
        _createdObjects.Clear();
    }

    [Test]
    public void Value_NormalizesAndKeepsArithmeticComparable() {
        var value = new Value(1500d);

        Assert.That(value.Base.Degree, Is.EqualTo(1));
        Assert.That(value.Stored, Is.EqualTo(1.5d).Within(0.0001d));
        Assert.That((value + new Value(500d)).ToDouble(), Is.EqualTo(2000d).Within(0.001d));
        Assert.That((value - new Value(250d))?.ToDouble(), Is.EqualTo(1250d).Within(0.001d));
        Assert.That((value * 2d).ToDouble(), Is.EqualTo(3000d).Within(0.001d));
        Assert.That(new Value(1d) - new Value(2d), Is.Null);
    }

    [Test]
    public void StatResolver_AppliesModifiersAndClampsRiskyStats() {
        var definition = CreateBuilding("Mayor", GovernmentInteractionType.MayorOffice, click: 10, income: 5, frequency: 1, price: 100);
        var state = new BuildingState(definition, 0);
        var resolver = new StatResolver();

        var result = resolver.Resolve(state, new List<StatModifier> {
            new() { Stat = StatType.ClickIncome, Operation = ModifierOp.AddFlat, Value = 5 },
            new() { Stat = StatType.ClickIncome, Operation = ModifierOp.AddPercent, Value = 1 },
            new() { Stat = StatType.ClickIncome, Operation = ModifierOp.Multiply, Value = 2 },
            new() { Stat = StatType.Frequency, Operation = ModifierOp.AddFlat, Value = -5 },
            new() { Stat = StatType.CriticalChance, Operation = ModifierOp.AddFlat, Value = 5 },
            new() { Stat = StatType.CriticalMultiplier, Operation = ModifierOp.Override, Value = 3, Priority = 1 },
            new() { Stat = StatType.CriticalMultiplier, Operation = ModifierOp.Override, Value = 7, Priority = 10 }
        });

        Assert.That(result.ClickIncome.ToDouble(), Is.EqualTo(60d).Within(0.001d));
        Assert.That(result.Frequency, Is.EqualTo(0.01f).Within(0.0001f));
        Assert.That(result.CriticalChance, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(result.CriticalMultiplier, Is.EqualTo(7f).Within(0.0001f));
    }

    [Test]
    public void Storage_AddSpendAffordAndLoadBothSaveFormats() {
        var storage = new Storage();
        var price = new Price(
            new Price.Entry(GovernmentInteractionType.MayorOffice, new Value(20)),
            new Price.Entry(GovernmentInteractionType.Court, new Value(5)));

        storage.AddMoney(GovernmentInteractionType.MayorOffice, new Value(25));
        storage.AddMoney(GovernmentInteractionType.Court, new Value(5));

        Assert.That(storage.CanAfford(price), Is.True);
        storage.Spend(price);
        AssertValue(storage.GetByType(GovernmentInteractionType.MayorOffice), 5);
        AssertValue(storage.GetByType(GovernmentInteractionType.Court), 0);

        var loaded = new Storage();
        loaded.Load(storage.Save());
        AssertValue(loaded.GetByType(GovernmentInteractionType.MayorOffice), 5);

        loaded.Load(new JObject {
            ["Money"] = new JArray(new JObject {
                ["type"] = GovernmentInteractionType.Archive.ToString(),
                ["amount"] = 42d
            })
        });
        AssertValue(loaded.GetByType(GovernmentInteractionType.Archive), 42);
    }

    [Test]
    public void BuildingPurchase_SpendsAccumulatedPriceAndInvalidatesBuildings() {
        var building = CreateBuilding("Mayor", GovernmentInteractionType.MayorOffice, click: 1, income: 1, frequency: 1, price: 10);
        var watcher = new BuildingWatcherService(new List<BuildingDefinition> { building });
        var invalidation = new InvalidationService(watcher.BuildingsByName);
        var upgradeService = new BuildingUpgradeService(invalidation, watcher);
        var storage = new Storage();
        var economy = new EconomyService(new SessionContext(0, 0, 0, 0, 0, 0), storage, watcher, upgradeService, new ProviderRegistryService());
        var upgradesRaised = 0;
        Track(upgradeService.OnBuildingUpgrade.Subscribe(_ => upgradesRaised++));
        storage.AddMoney(GovernmentInteractionType.MayorOffice, new Value(50));

        Assert.That(economy.CanPurchaseBuilding("Mayor", 2), Is.True);
        economy.PurchaseBuilding("Mayor", 2);

        Assert.That(watcher.GetBuildingState("Mayor").Level, Is.EqualTo(2));
        Assert.That(upgradesRaised, Is.EqualTo(1));
        AssertValue(storage.GetByType(GovernmentInteractionType.MayorOffice), 30);
        Assert.That(watcher.GetBuildingState("Mayor").IsDirty, Is.True);
    }

    [Test]
    public void UnlockService_DefaultsMayorAndPublishesNewUnlock() {
        var unlockService = Track(new UnlockService());
        var observed = false;
        Track(unlockService.ObserveItemUnlocked(GovernmentInteractionType.Archive.ToString()).Subscribe(value => observed = value));

        Assert.That(unlockService.IsItemUnlocked(nameof(GovernmentInteractionType.MayorOffice)), Is.True);
        unlockService.UnlockItem(GovernmentInteractionType.Archive.ToString());

        Assert.That(observed, Is.True);
    }

    [Test]
    public void PracticeService_OfferConfirmRecycleAndSaveLoad() {
        var commonA = CreatePractice("common-a", PracticeRarity.Common, 1f);
        var commonB = CreatePractice("common-b", PracticeRarity.Common, 1f);
        var invalidation = new InvalidationService(new Dictionary<string, BuildingState>());
        var service = Track(new PracticeService(new[] { commonA, commonB }, null, invalidation, new SessionContext(0, 0, 0, 0, 0, 0)));

        Assert.That(service.BeginResearchOffer(PracticeRarity.Common), Is.True);
        Assert.That(service.ConfirmSelectedOffer(), Is.True);
        Assert.That(service.OwnedPracticeDefinitions.Count, Is.EqualTo(1));

        Assert.That(service.BeginResearchOffer(PracticeRarity.Common), Is.True);
        Assert.That(service.RecycleSelectedOffer(), Is.True);

        var loaded = Track(new PracticeService(new[] { commonA, commonB }, null, invalidation, new SessionContext(0, 0, 0, 0, 0, 0)));
        loaded.Load(service.Save());
        Assert.That(loaded.OwnedPracticeDefinitions.Count, Is.EqualTo(1));
    }

    [Test]
    public void RecycleService_CreatesAndRestoresRecycleModifier() {
        var building = CreateBuilding("Mayor", GovernmentInteractionType.MayorOffice, click: 1, income: 1, frequency: 1, price: 1);
        var watcher = new BuildingWatcherService(new List<BuildingDefinition> { building });
        var invalidation = new InvalidationService(watcher.BuildingsByName);
        var practice = CreatePractice("common-a", PracticeRarity.Common, 1f);
        var practiceService = Track(new PracticeService(new[] { practice }, null, invalidation, new SessionContext(0, 0, 0, 0, 0, 0)));
        var recycle = Track(new RecycleService(practiceService, watcher, null));
        recycle.StartService();

        Assert.That(practiceService.BeginResearchOffer(PracticeRarity.Common), Is.True);
        Assert.That(practiceService.RecycleSelectedOffer(), Is.True);
        Assert.That(recycle.RecycleModifiers, Has.Count.EqualTo(1));

        var loaded = Track(new RecycleService(practiceService, watcher, null));
        loaded.Load(recycle.Save());
        Assert.That(loaded.RecycleModifiers, Has.Count.EqualTo(1));
    }

    [Test]
    public void StatisticsAndAchievements_SaveRestoreAndUnlock() {
        var key = new StatisticKey<int>("clicks");
        var statistics = Track(new StatisticsService());
        statistics.Register(key, 1);
        ((IStatisticsWriter)statistics).Increment(key);
        Assert.That(statistics.Get(key), Is.EqualTo(2));

        var loadedStats = Track(new StatisticsService());
        loadedStats.Register(key, 0);
        loadedStats.Load(statistics.Save());
        Assert.That(loadedStats.Get(key), Is.EqualTo(2));

        var achievement = Track(new FakeAchievement("first"));
        var achievementService = Track(new AchievementService(new[] { achievement }));
        var unlockedId = string.Empty;
        Track(achievementService.Unlocked.Subscribe(unlocked => unlockedId = unlocked.Id));
        achievementService.StartService();
        achievement.Complete();
        Assert.That(unlockedId, Is.EqualTo("first"));

        Assert.Throws<ArgumentException>(() => new AchievementService(new[] { new FakeAchievement("dup"), new FakeAchievement("dup") }));
    }

    [Test]
    public void StatisticsService_SaveOmitsNonPersistentStatistics() {
        var statistics = Track(new StatisticsService());
        statistics.Register(StatisticKeys.TotalClicks);
        statistics.Register(StatisticKeys.PassiveResourceIncomePerSecond, new Wallet(), false);
        ((IStatisticsWriter)statistics).Increment(StatisticKeys.TotalClicks);
        ((IStatisticsWriter)statistics).Set(StatisticKeys.PassiveResourceIncomePerSecond, new Wallet {
            MayorWallet = new Value(10)
        });

        var values = (JObject)statistics.Save()["values"];

        Assert.That(values.ContainsKey(StatisticKeys.TotalClicks.Id), Is.True);
        Assert.That(values.ContainsKey(StatisticKeys.PassiveResourceIncomePerSecond.Id), Is.False);
    }

    [Test]
    public void ResourceIncomePerSecondCalculator_SumsActiveBuildingsByResource() {
        var mayor = CreateBuilding("Mayor", GovernmentInteractionType.MayorOffice, click: 1, income: 3, frequency: 2, price: 1);
        var police = CreateBuilding("Police", GovernmentInteractionType.PoliceStation, click: 1, income: 5, frequency: 4, price: 1);
        var archive = CreateBuilding("Archive", GovernmentInteractionType.Archive, click: 1, income: 100, frequency: 10, price: 1);
        var watcher = new BuildingWatcherService(new List<BuildingDefinition> { mayor, police, archive });
        watcher.GetBuildingState("Mayor").Level = 1;
        watcher.GetBuildingState("Police").Level = 1;
        watcher.GetBuildingState("Archive").Level = 0;
        var invalidation = new InvalidationService(watcher.BuildingsByName);
        var upgradeService = new BuildingUpgradeService(invalidation, watcher);
        var storage = new Storage();
        var context = new SessionContext(0, 0, 0, 0, 0, 0);
        var economy = new EconomyService(context, storage, watcher, upgradeService, new ProviderRegistryService());

        var result = ResourceIncomePerSecondCalculator.Calculate(
            watcher.BuildingsByName.Values,
            economy,
            new StateBenefitCalculationService(context));

        AssertValue(result.MayorWallet, 6);
        AssertValue(result.PoliceWallet, 20);
        AssertValue(result.ArchiveWallet, 0);
        AssertValue(result.CourtWallet, 0);
    }

    [Test]
    public void NotificationService_ValidatesAndPublishesNotifications() {
        var service = Track(new NotificationService());
        NotificationRequest received = default;
        Track(service.OnNotification.Subscribe(notification => received = notification));

        service.Push(new NotificationRequest("Ready", "Message", NotificationType.Info, NotificationPriority.High));

        Assert.That(received.Title, Is.EqualTo("Ready"));
        Assert.Throws<ArgumentException>(() => service.Push(new NotificationRequest("", "Message", NotificationType.Info, NotificationPriority.Low)));
    }

    [Test]
    public void ProviderRegistry_ReturnsRegisteredProviderAndCollectsModifiers() {
        var registry = new ProviderRegistryService();
        var provider = new FakeModifierProvider(new StatModifier { Stat = StatType.Income, Operation = ModifierOp.AddFlat, Value = 2 });
        registry.RegisterProvider(provider);
        var output = new List<StatModifier>();

        registry.FetchModifiers(new SessionContext(0, 0, 0, 0, 0, 0), null, output);

        Assert.That(registry.GetProvider<FakeModifierProvider>(), Is.SameAs(provider));
        Assert.That(output, Has.Count.EqualTo(1));
        Assert.Throws<ArgumentException>(() => registry.GetProvider<MissingProvider>());
    }

    private T Track<T>(T disposable) where T : IDisposable {
        _disposables.Add(disposable);
        return disposable;
    }

    private BuildingDefinition CreateBuilding(string name, GovernmentInteractionType type, double click, double income, double frequency, double price) {
        var definition = ScriptableObject.CreateInstance<BuildingDefinition>();
        _createdObjects.Add(definition);
        definition.Name = name;
        definition.Type = type;
        definition.ClickIncome = new ConstantFormula { BaseValue = click };
        definition.Income = new ConstantFormula { BaseValue = income };
        definition.Frequency = new ConstantFormula { BaseValue = frequency };
        definition.StabilityModifier = new ConstantFormula { BaseValue = 1 };
        definition.StabilityModifierMultiplier = new ConstantFormula { BaseValue = 1 };
        definition.MultiplierCoefficient = new ConstantFormula { BaseValue = 1 };
        definition.CriticalChance = new ConstantFormula { BaseValue = 0 };
        definition.CriticalMultiplier = new ConstantFormula { BaseValue = 1 };
        definition.Cost = CreateCostResolver(type, price);
        return definition;
    }

    private Practice CreatePractice(string id, PracticeRarity rarity, float weight) {
        var practice = ScriptableObject.CreateInstance<Practice>();
        _createdObjects.Add(practice);
        SetPrivateField(practice, "_id", id);
        SetPrivateField(practice, "_rarity", rarity);
        SetPrivateField(practice, "_weight", weight);
        return practice;
    }

    private static CostResolver CreateCostResolver(GovernmentInteractionType type, double amount) {
        var resolver = new CostResolver();
        var costItemType = typeof(CostResolver).GetNestedType("CostItem", BindingFlags.NonPublic);
        var costItem = Activator.CreateInstance(costItemType, true);
        var condition = new ConstCondition();
        typeof(ConstCondition).GetField("_constant", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(condition, true);
        costItemType.GetField("Condition", BindingFlags.Instance | BindingFlags.Public).SetValue(costItem, condition);
        costItemType.GetField("Formula", BindingFlags.Instance | BindingFlags.Public).SetValue(costItem, new ConstantFormula { BaseValue = amount });
        costItemType.GetField("Type", BindingFlags.Instance | BindingFlags.Public).SetValue(costItem, type);
        var listType = typeof(List<>).MakeGenericType(costItemType);
        var list = (IList)Activator.CreateInstance(listType);
        list.Add(costItem);
        typeof(CostResolver).GetField("_costItems", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(resolver, list);
        return resolver;
    }

    private static void SetPrivateField(object target, string fieldName, object value) {
        target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    private static void AssertValue(Value value, double expected) {
        Assert.That(value.ToDouble(), Is.EqualTo(expected).Within(0.001d));
    }

    private sealed class FakeModifierProvider : IModifierProvider {
        private readonly StatModifier _modifier;
        public FakeModifierProvider(StatModifier modifier) => _modifier = modifier;
        public void Collect(ISessionContext context, BuildingState building, List<StatModifier> modifiers) => modifiers.Add(_modifier);
    }

    private sealed class MissingProvider : IModifierProvider {
        public void Collect(ISessionContext context, BuildingState building, List<StatModifier> modifiers) { }
    }

    private sealed class FakeAchievement : IAchievement {
        private readonly ReactiveProperty<bool> _completed = new(false);

        public FakeAchievement(string id) => Id = id;
        public string Id { get; }
        public string Name => Id;
        public string Description => Id;
        public ReadOnlyReactiveProperty<bool> IsCompleted => _completed;
        public bool IsLoadedAsCompleted { get; private set; }
        public Observable<float> Progress => _completed.Select(value => value ? 1f : 0f);
        public Observable<string> ProgressText => _completed.Select(value => value ? "done" : "open");
        public void Start() { }
        public void RestoreCompleted() { IsLoadedAsCompleted = true; _completed.Value = true; }
        public void Complete() => _completed.Value = true;
        public void Dispose() => _completed.Dispose();
    }
}
