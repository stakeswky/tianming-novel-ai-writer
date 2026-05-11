using System;
using System.Text.RegularExpressions;

namespace TM.Framework.Common.Helpers
{
    public static class EntityNameNormalizeHelper
    {
        public static string StripBracketAnnotation(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var t = name.Trim();
            t = Regex.Replace(t, @"\s*[\(（\[【].*?[\)）\]】]\s*$", string.Empty);
            return t.Trim();
        }

        public static bool NameExistsInContent(string content, string fullName)
        {
            if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(fullName))
                return false;

            if (content.Contains(fullName, StringComparison.OrdinalIgnoreCase))
                return true;

            var primaryName = StripBracketAnnotation(fullName);
            if (!string.IsNullOrWhiteSpace(primaryName) && primaryName != fullName
                && content.Contains(primaryName, StringComparison.OrdinalIgnoreCase))
                return true;

            var aliasMatches = Regex.Matches(fullName, @"[\(（\[【](.+?)[\)）\]】]");
            foreach (Match m in aliasMatches)
            {
                var alias = m.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(alias) && alias.Length >= 2
                    && content.Contains(alias, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
