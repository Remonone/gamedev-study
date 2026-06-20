using System.Collections.Generic;
using Types.Modifiers.Definitions.Buildings;
using Types.Modifiers.Definitions;
using Types.Modifiers.Definitions.Context;
using Types.Modifiers.Definitions.Upgrades;
using Types.Modifiers.Definitions.Upgrades.Effects;

namespace Economy.Providers {
    public class UpgradeModifierProvider : IModifierProvider {
        private readonly Dictionary<string, ActiveUpgradeModifiers> _activeUpgradesById = new();
        public void Collect(ISessionContext context, BuildingState building, List<StatModifier> modifiers) {
            foreach (var upgrade in _activeUpgradesById.Values) {
                IModifierContext modifierContext = CollectContext(context, upgrade.Level);
                foreach (var definition in upgrade.Definitions) {
                    if (definition == null || definition.Target == null) {
                        continue;
                    }
                    
                    var state = definition.Resolve(building, modifierContext);
                    if (!state.HasValue) {
                        continue;
                    }
                    modifiers.Add(state.Value);
                }
            }
        }
        
        public void AddOrUpdate(UpgradeNodeState upgrade) {
            if (upgrade?.Definition == null) return;
                
            var definitions = new List<ModifierDefinition>();

            foreach (var effect in upgrade.Definition.Effects) {
                if (effect is not ModifierUpgradeEffect modifierEffect) continue;
                    
                definitions.AddRange(modifierEffect.Rules);
            }
            
            _activeUpgradesById[upgrade.Definition.Id] = new ActiveUpgradeModifiers(upgrade.Definition.Id, upgrade.Level, definitions);
        }

        private IModifierContext CollectContext(ISessionContext session, int level) {
            var context = new ModifierContext();
            context.Add(new SessionCapability(session));
            context.Add(new LevelCapability(level));
            return context;
        }

        private sealed class ActiveUpgradeModifiers {
            public string UpgradeId { get; }
            public int Level { get; }
            public IReadOnlyList<ModifierDefinition> Definitions;
            
            public ActiveUpgradeModifiers(string upgradeId, int level, IReadOnlyList<ModifierDefinition> definitions) {
                UpgradeId = upgradeId;
                Level = level;
                Definitions = definitions;
            }
        }
    }
}