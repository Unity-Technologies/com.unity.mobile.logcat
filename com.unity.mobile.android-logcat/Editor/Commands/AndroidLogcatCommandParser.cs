using System;
using System.Collections.Generic;
using System.Text;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// Splitting and classification of raw command strings entered by the user.
    /// Deliberately free of any UI dependency so the behaviour can be unit tested.
    /// </summary>
    internal static class AndroidLogcatCommandParser
    {
        internal static readonly char[] NewlineChars = { '\r', '\n' };

        const string kAdbPrefix = "adb ";

        /// <summary>
        /// Splits a possibly multiline command block into individual trimmed, non empty commands.
        /// Handles CR, LF and CRLF line endings.
        /// </summary>
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

        /// <summary>
        /// Splits captured process output into display lines.
        ///
        /// Carriage returns are stripped rather than treated as separators: adb on Windows emits
        /// "\r\r\n" for shell command output, so splitting on '\r' would yield two empty lines per
        /// real line (rendering as visibly double/triple spaced output), and leaving them in makes
        /// UIToolkit render each stray '\r' as an extra line break. '\r' is never meaningful here,
        /// it's purely a newline translation artifact.
        ///
        /// Genuinely blank lines (from "\n\n") are preserved, since tools like dumpsys use them as
        /// section separators.
        /// </summary>
        internal static string[] SplitOutputLines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new string[0];

            return text.Replace("\r", string.Empty).Split('\n');
        }

        /// <summary>
        /// True when the command should be routed through the package's adb wrapper.
        /// </summary>
        internal static bool IsAdbCommand(string command)
        {
            return !string.IsNullOrEmpty(command) &&
                command.TrimStart().StartsWith(kAdbPrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Strips the leading "adb " from a command, returning the remaining arguments.
        /// </summary>
        internal static string StripAdbPrefix(string command)
        {
            if (!IsAdbCommand(command))
                return command;
            return command.TrimStart().Substring(kAdbPrefix.Length).Trim();
        }

        /// <summary>
        /// True when the arguments already target a specific device, in which case we must not
        /// inject our own -s flag.
        /// </summary>
        internal static bool SpecifiesDevice(string adbArguments)
        {
            if (string.IsNullOrEmpty(adbArguments))
                return false;

            foreach (var token in Tokenize(adbArguments))
            {
                if (token == "-s" || token == "--serial")
                    return true;
                // Stop at the first non-flag token; -s only counts as a global option before the subcommand.
                if (!token.StartsWith("-", StringComparison.Ordinal))
                    return false;
            }
            return false;
        }

        /// <summary>
        /// Splits a command line into tokens, honouring double and single quotes so that paths
        /// containing spaces survive intact. Quote characters themselves are removed.
        /// </summary>
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

        /// <summary>
        /// Splits a non-adb command into an executable and its argument string, honouring quotes so
        /// that executables with spaces in their path (common on Windows) are handled correctly.
        /// Returns false when no executable could be determined.
        /// </summary>
        internal static bool TrySplitExecutable(string command, out string executable, out string arguments)
        {
            executable = null;
            arguments = null;

            var tokens = Tokenize(command);
            if (tokens.Count == 0)
                return false;

            executable = tokens[0];

            // Re-quote any argument that contains whitespace so the child process sees the same value.
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
