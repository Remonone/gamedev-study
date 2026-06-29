using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Economy.Providers;
using NUnit.Framework;
using Services;
using Services.Events;
using Services.Player;
using Types.Buildings;
using Types.Enums;
using Types.Events.Global;
using Types.Modifiers;
using Types.Modifiers.Cost;
using Types.Modifiers.Cost.Condition;
using Types.Modifiers.Cost.Formula;
using Types.Values;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class GameMechanicsPlayModeTests {
    private readonly List<UnityEngine.Object> _createdObjects = new();
    private readonly List<System.IDisposable> _disposables = new();

    [TearDown]
    public void TearDown() {
        foreach (var disposable in _disposables) disposable.Dispose();
        _disposables.Clear();
        foreach (var obj in _createdObjects) {
            if (obj != null) Object.Destroy(obj);
        }
        _createdObjects.Clear();
    }

    [UnityTest]
    public IEnumerator GlobalEventService_StartsAndEndsEventAcrossFrames() {
        var invalidation = new InvalidationService(new Dictionary<string, BuildingState>());
        var globalEvent = ScriptableObject.CreateInstance<GlobalEvent>();
        _createdObjects.Add(globalEvent);
        SetPrivateField(globalEvent, "_durationSeconds", 0.02f);
        var service = Track(new GlobalEventService(new[] { globalEvent }, invalidation, 1f));

        service.StartService();
        typeof(GlobalEventService)
            .GetMethod("StartRandomEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(service, null);
        yield return null;
        Assert.That(service.ActiveEvent, Is.SameAs(globalEvent));

        yield return new WaitForSeconds(0.05f);
        Assert.That(service.ActiveEvent, Is.Null);
    }

    [UnityTest]
    public IEnumerator TickService_ProducesPassiveIncomeAcrossFrames() {
        var definition = CreateBuilding("Mayor", GovernmentInteractionType.MayorOffice, income: 3, frequency: 60, price: 1);
        var watcher = new BuildingWatcherService(new List<BuildingDefinition> { definition });
        var state = watcher.GetBuildingState("Mayor");
        state.Level = 1;
        var invalidation = new InvalidationService(watcher.BuildingsByName);
        var storage = new Storage();
        var upgrade = new BuildingUpgradeService(invalidation, watcher);
        var context = new SessionContext(0, 0, 0, 0, 0, 0);
        var economy = new EconomyService(context, storage, watcher, upgrade, new ProviderRegistryService(), invalidation);
        var tickService = Track(new TickService(economy, watcher, storage, new StateBenefitCalculationService(context)));

        tickService.StartService();
        yield return new WaitForSeconds(0.05f);

        Assert.That(storage.GetByType(GovernmentInteractionType.MayorOffice), Is.GreaterThan(Value.Zero));
    }

    private T Track<T>(T disposable) where T : System.IDisposable {
        _disposables.Add(disposable);
        return disposable;
    }

    private BuildingDefinition CreateBuilding(string name, GovernmentInteractionType type, double income, double frequency, double price) {
        var definition = ScriptableObject.CreateInstance<BuildingDefinition>();
        _createdObjects.Add(definition);
        definition.Name = name;
        definition.Type = type;
        definition.ClickIncome = new ConstantFormula { BaseValue = 1 };
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

    private static CostResolver CreateCostResolver(GovernmentInteractionType type, double amount) {
        var resolver = new CostResolver();
        var costItemType = typeof(CostResolver).GetNestedType("CostItem", BindingFlags.NonPublic);
        var costItem = System.Activator.CreateInstance(costItemType, true);
        var condition = new ConstCondition();
        typeof(ConstCondition).GetField("_constant", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(condition, true);
        costItemType.GetField("Condition", BindingFlags.Instance | BindingFlags.Public).SetValue(costItem, condition);
        costItemType.GetField("Formula", BindingFlags.Instance | BindingFlags.Public).SetValue(costItem, new ConstantFormula { BaseValue = amount });
        costItemType.GetField("Type", BindingFlags.Instance | BindingFlags.Public).SetValue(costItem, type);
        var listType = typeof(List<>).MakeGenericType(costItemType);
        var list = (System.Collections.IList)System.Activator.CreateInstance(listType);
        list.Add(costItem);
        typeof(CostResolver).GetField("_costItems", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(resolver, list);
        return resolver;
    }

    private static void SetPrivateField(object target, string fieldName, object value) {
        target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
