using System;
using System.Collections.Generic;
using System.Text;
using Data.Modifiers;
using Data.Upgrades;
using Utils;

namespace Presentation {
    public static class UpgradeDescriptionFormatter {
        public static string Format(UpgradeNodeDefinition definition, int currentLevel) {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            string description = definition.Description ?? string.Empty;
            if (description.Length == 0 || definition.Modifiers == null) return description;

            Dictionary<string, NumericModifierDefinition> modifiers = CollectModifiers(definition.Modifiers);
            if (modifiers.Count == 0) return description;

            int nextLevel = definition.GetNextPreviewLevel(currentLevel);
            var result = new StringBuilder(description.Length);
            int position = 0;
            while (position < description.Length) {
                int tokenStart = description.IndexOf("${", position, StringComparison.Ordinal);
                if (tokenStart < 0) {
                    result.Append(description, position, description.Length - position);
                    break;
                }

                result.Append(description, position, tokenStart - position);
                int tokenEnd = description.IndexOf('}', tokenStart + 2);
                if (tokenEnd < 0) {
                    result.Append(description, tokenStart, description.Length - tokenStart);
                    break;
                }

                string id = description.Substring(tokenStart + 2, tokenEnd - tokenStart - 2);
                if (modifiers.TryGetValue(id, out NumericModifierDefinition modifier)) {
                    Value current = modifier.EvaluateAtLevel(currentLevel);
                    Value next = nextLevel == currentLevel ? current : modifier.EvaluateAtLevel(nextLevel);
                    result.Append(FormatValues(current, next));
                }
                else {
                    result.Append(description, tokenStart, tokenEnd - tokenStart + 1);
                }

                position = tokenEnd + 1;
            }

            return result.ToString();
        }

        private static Dictionary<string, NumericModifierDefinition> CollectModifiers(
            ModifierDefinition[] definitions) {
            var result = new Dictionary<string, NumericModifierDefinition>(StringComparer.Ordinal);
            for (int definitionIndex = 0; definitionIndex < definitions.Length; definitionIndex++) {
                ModifierDefinition definition = definitions[definitionIndex];
                if (definition?.NumericModifiers == null) continue;
                for (int modifierIndex = 0; modifierIndex < definition.NumericModifiers.Count; modifierIndex++) {
                    NumericModifierDefinition modifier = definition.NumericModifiers[modifierIndex];
                    if (modifier != null && !string.IsNullOrWhiteSpace(modifier.Id)) {
                        result.TryAdd(modifier.Id, modifier);
                    }
                }
            }

            return result;
        }

        private static string FormatValues(Value current, Value next) {
            int comparison = next.CompareTo(current);
            if (comparison == 0) return $"{current}(0)";

            if (comparison > 0) {
                Value delta = (next - current).Value;
                return $"{current}({delta})";
            }

            Value decrease = (current - next).Value;
            return $"{current}(-{decrease})";
        }
    }
}
