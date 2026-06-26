using System.Text.RegularExpressions;
using Types.Buildings;
using UnityEngine;

namespace Types.Modifiers.Target {
    [CreateAssetMenu(fileName = "StringPatternTarget", menuName = "Clicker/Modifiers/Target/By String Pattern", order = 0)]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, null, "Assembly-CSharp")]
    public class TargetByStringPattern : ModifierTarget {

        [Tooltip("Wildcard pattern matched against BuildingDefinition.Name. Use * for any sequence and ? for one character.")]
        public string Pattern;

        [Tooltip("If enabled, uppercase and lowercase letters must match exactly.")]
        public bool CaseSensitive;

        private string _cachedPattern;
        private bool _cachedCaseSensitive;
        private Regex _cachedRegex;

        public override bool Matches(BuildingState building) {
            if (building == null || building.Definition == null || string.IsNullOrEmpty(Pattern)) {
                return false;
            }

            var buildingName = building.Definition.Name;
            if (buildingName == null) {
                return false;
            }

            return GetRegex().IsMatch(buildingName);
        }

        private Regex GetRegex() {
            if (_cachedRegex != null && _cachedPattern == Pattern && _cachedCaseSensitive == CaseSensitive) {
                return _cachedRegex;
            }

            _cachedPattern = Pattern;
            _cachedCaseSensitive = CaseSensitive;

            var options = RegexOptions.CultureInvariant;
            if (!CaseSensitive) {
                options |= RegexOptions.IgnoreCase;
            }

            _cachedRegex = new Regex(ToRegexPattern(Pattern), options);
            return _cachedRegex;
        }

        private static string ToRegexPattern(string pattern) {
            return "^" + Regex.Escape(pattern)
                .Replace(@"\*", ".*")
                .Replace(@"\?", ".") + "$";
        }
    }
}
