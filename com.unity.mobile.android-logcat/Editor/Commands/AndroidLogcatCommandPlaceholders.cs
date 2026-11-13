using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Unity.Android.Logcat
{
    internal static class AndroidLogcatCommandPlaceholders
    {
        internal static readonly Regex PlaceholderRegex = new Regex(@"<([^<>]+)>", RegexOptions.Compiled);

        internal class Placeholder
        {
            internal string Token;
            internal string Value;

            internal Placeholder(string token, string value)
            {
                Token = token;
                Value = value;
            }
        }

        internal static bool HasPlaceholders(string command)
        {
            return !string.IsNullOrEmpty(command) && PlaceholderRegex.IsMatch(command);
        }

        internal static List<Placeholder> Parse(string command, Func<string, string> defaultValueProvider = null)
        {
            var result = new List<Placeholder>();
            if (string.IsNullOrEmpty(command))
                return result;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in PlaceholderRegex.Matches(command))
            {
                var token = match.Groups[1].Value;
                if (!seen.Add(token))
                    continue;

                var defaultValue = defaultValueProvider != null ? defaultValueProvider(token) : string.Empty;
                result.Add(new Placeholder(token, defaultValue ?? string.Empty));
            }
            return result;
        }

        internal static string Resolve(string command, IEnumerable<Placeholder> placeholders)
        {
            if (string.IsNullOrEmpty(command))
                return command;

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (placeholders != null)
            {
                foreach (var placeholder in placeholders)
                {
                    if (placeholder == null || string.IsNullOrEmpty(placeholder.Token))
                        continue;
                    values[placeholder.Token] = placeholder.Value ?? string.Empty;
                }
            }

            return PlaceholderRegex.Replace(command, match =>
            {
                var token = match.Groups[1].Value;
                return values.TryGetValue(token, out var value) ? value : match.Value;
            });
        }
    }
}
