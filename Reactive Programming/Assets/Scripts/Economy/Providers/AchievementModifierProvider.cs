using System.Collections.Generic;
using Types.Modifiers.Definitions;
using Types.Modifiers.Definitions.Achievements;
using Types.Modifiers.Definitions.Buildings;
using Types.Modifiers.Definitions.Context;
using UnityEngine;

namespace Economy.Providers {
    public class AchievementModifierProvider : IModifierProvider {
        
        private readonly HashSet<AchievementModifier> _achievementModifiers = new();

        public void AddAchievementModifier(AchievementModifier modifier) {
            if (!_achievementModifiers.Add(modifier)) {
                Debug.LogError($"Achievement modifier {modifier} already exists");
            }
        }
        
        public void Collect(ISessionContext context, BuildingState building, List<StatModifier> modifiers) {
            foreach (var achievement in _achievementModifiers) { 
                ModifierContext modifierContext = new ModifierContext();
                modifierContext.Add(new SessionCapability(context));
                foreach (var definition in achievement.Modifiers) {
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
        
        
    }
}