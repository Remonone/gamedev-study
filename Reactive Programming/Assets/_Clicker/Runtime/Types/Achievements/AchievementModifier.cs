using System;
using System.Collections.Generic;
using Types.Modifiers.Definitions;
using UnityEngine;

namespace Types.Achievements {
    [CreateAssetMenu(fileName = "Achievement Modifier", menuName = "Modifiers/Achievement Modifier", order = 0)]
    public class AchievementModifier : ScriptableObject, IEquatable<AchievementModifier> {
        public string TrackedAchievement;
        public List<ModifierDefinition> Modifiers;

        public bool Equals(AchievementModifier other) {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) && TrackedAchievement == other.TrackedAchievement;
        }

        public override bool Equals(object obj) {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((AchievementModifier)obj);
        }

        public override int GetHashCode() {
            return HashCode.Combine(base.GetHashCode(), TrackedAchievement, Modifiers);
        }
    }
}