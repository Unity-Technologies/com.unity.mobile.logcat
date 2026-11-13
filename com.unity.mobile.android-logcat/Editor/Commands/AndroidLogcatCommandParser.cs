using System;
using System.Collections.Generic;
using System.Text;

namespace Unity.Android.Logcat
{
    internal static class AndroidLogcatCommandParser
    {
        internal static readonly char[] NewlineChars = { '\r', '\n' };

        const string kAdbPrefix = "adb ";

        internal static List<string> SplitLines(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text))
                return result;

            foreach (var line in text.Split(NewlineChars, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0)
                    result.Add(trimmed);
            }
            return result;
        }

        internal static string[] SplitOutputLines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new string[0];

            return text.Replace("\r", string.Empty).Split('\n');
        }

        internal static bool IsAdbCommand(string command)
        {
            return !string.IsNullOrEmpty(command) &&
                command.TrimStart().StartsWith(kAdbPrefix, StringComparison.OrdinalIgnoreCase);
        }

        internal static string StripAdbPrefix(string command)
        {
            if (!IsAdbCommand(command))
                return command;
            return command.TrimStart().Substring(kAdbPrefix.Length).Trim();
        }

        internal static bool SpecifiesDevice(string adbArguments)
        {
            if (string.IsNullOrEmpty(adbArguments))
                return false;

            foreach (var token in Tokenize(adbArguments))
            {
                if (token == "-s" || token == "--serial")
                    return true;
                if (!token.StartsWith("-", StringComparison.Ordinal))
                    return false;
            }
            return false;
        }

        internal static List<string> Tokenize(string commandLine)
        {
            var tokens = new List<string>();
            if (string.IsNullOrEmpty(commandLine))
                return tokens;

            var current = new StringBuilder();
            var quoteChar = '\0';
            var inToken = false;

            foreach (var c in commandLine)
            {
                if (quoteChar != '\0')
                {
                    if (c == quoteChar)
                        quoteChar = '\0';
                    else
                        current.Append(c);
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    quoteChar = c;
                    inToken = true;
                    continue;
                }

                if (char.IsWhiteSpace(c))
                {
                    if (inToken)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                        inToken = false;
                    }
                    continue;
                }

                current.Append(c);
                inToken = true;
            }

            if (inToken)
                tokens.Add(current.ToString());

            return tokens;
        }

        internal static bool TrySplitExecutable(string command, out string executable, out string arguments)
        {
            executable = null;
            arguments = null;

            var tokens = Tokenize(command);
            if (tokens.Count == 0)
                return false;

            executable = tokens[0];

            var sb = new StringBuilder();
            for (int i = 1; i < tokens.Count; i++)
            {
                if (sb.Length > 0)
                    sb.Append(' ');

                var token = tokens[i];
                if (token.IndexOf(' ') >= 0 || token.IndexOf('\t') >= 0)
                    sb.Append('"').Append(token).Append('"');
                else
                    sb.Append(token);
            }

            arguments = sb.ToString();
            return true;
        }
    }
}
