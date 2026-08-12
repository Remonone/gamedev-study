using System;
using System.Collections.Generic;
using System.Linq;

namespace SigningGame.Editor.UpgradeTree {
    internal static class UpgradeTreeEditorValidation {
        private static readonly char[] _Forbidden = { '<', '>', ':', '"', '/', '\\', '|', '?', '*', '[', ']' };
        private static readonly HashSet<string> _ReservedNames = BuildReservedNames();

        internal static bool TryValidateSegment(string value, string label, out string error) {
            error = null;
            if (string.IsNullOrWhiteSpace(value)) {
                error = $"{label} cannot be empty.";
                return false;
            }

            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal)) {
                error = $"{label} cannot start or end with whitespace.";
                return false;
            }

            if (value is "." or "..") {
                error = $"{label} cannot be '{value}'.";
                return false;
            }

            if (value.EndsWith(".", StringComparison.Ordinal) || value.EndsWith(" ", StringComparison.Ordinal)) {
                error = $"{label} cannot end with a dot or space.";
                return false;
            }

            if (value.Any(character => character < 32 || _Forbidden.Contains(character))) {
                error = $"{label} contains a cross-platform forbidden character.";
                return false;
            }

            string basename = value.Split('.')[0];
            if (_ReservedNames.Contains(basename)) {
                error = $"{label} uses reserved filename '{basename}'.";
                return false;
            }

            return true;
        }

        internal static bool TryValidateRootSuffix(string suffix, out string normalized, out string error) {
            normalized = null;
            error = null;
            if (string.IsNullOrWhiteSpace(suffix)) {
                error = "Upgrade path cannot be empty.";
                return false;
            }

            if (suffix.Contains('\\') || suffix.StartsWith("/", StringComparison.Ordinal) ||
                suffix.StartsWith("Assets", StringComparison.OrdinalIgnoreCase)) {
                error = "Enter only the path after 'Assets/' and use '/' separators.";
                return false;
            }

            string[] segments = suffix.Split('/');
            if (segments.Length == 0 || segments.Any(string.IsNullOrEmpty)) {
                error = "Upgrade path contains an empty segment.";
                return false;
            }

            for (var index = 0; index < segments.Length; index++) {
                if (!TryValidateSegment(segments[index], $"Path segment {index + 1}", out error)) return false;
            }

            normalized = string.Join("/", segments);
            return true;
        }

        internal static bool PathsEqual(string left, string right) {
            return string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);
        }

        internal static string NormalizePath(string path) {
            return (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        }

        private static HashSet<string> BuildReservedNames() {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CON", "PRN", "AUX", "NUL" };
            for (var number = 1; number <= 9; number++) {
                result.Add($"COM{number}");
                result.Add($"LPT{number}");
            }

            return result;
        }
    }
}
