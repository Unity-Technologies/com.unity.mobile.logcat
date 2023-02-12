using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Text;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatPackageInfoParser
    {
        internal static readonly string FailedKey = "Failed";
        // Note: Using List instead of Dictionary in case of duplicate keys
        List<KeyValuePair<string, string>> m_Entries;

        internal List<KeyValuePair<string, string>> Entries => m_Entries;

        internal AndroidLogcatPackageInfoParser(string contents, string packageName)
        {
            m_Entries = new List<KeyValuePair<string, string>>();

            contents = contents.Replace("\r\n", "\n");

            // Note: Keep empty entries, helps us to determine block end
            var lines = contents.Split(new[] { '\n' }).ToArray();
            for (int i = 0; i < lines.Length; i++)
            {
                // Find block start
                if (!lines[i].Trim().StartsWith($"Package [{packageName}]"))
                    continue;

                var blockStart = i;
                var blockEnd = -1;
                for (var l = blockStart + 1; l < lines.Length; l++)
                {
                    blockEnd = l;
                    // Find block end
                    if (string.IsNullOrEmpty(lines[blockEnd].Trim()))
                    {
                        blockEnd--;
                        break;
                    }
                }

                var length = blockEnd - blockStart + 1;
                if (length <= 0)
                    return;

                var blockLines = lines.Skip(i).Take(length).ToArray();
                ParsePackageInformation(blockLines, packageName);
                return;
            }
        }

        private void ParsePackageInformation(string[] lines, string packageName)
        {
            if (lines.Length == 0)
                throw new System.Exception("No package info found");

            var regexPackageName = Regex.Escape(packageName);
            var title = new Regex($"Package.*{regexPackageName}.*\\:");

            if (!title.Match(lines[0]).Success)
                throw new Exception($"Expected 'Package [{packageName}] (<id>) :', but got '{lines[0]}'");

            var keyValueRegex = new Regex(@"\s+(?<key>\w+)\=(?<value>.*)");
            for (var i = 1; i < lines.Length;)
            {
                var l = lines[i];

                // Is it a permissions block?
                if (l.EndsWith("permissions:"))
                {
                    var key = l.Trim();
                    var values = ParsePermissionBlock(lines, i, out var blockLength);
                    m_Entries.Add(new KeyValuePair<string, string>(key, values));
                    i += blockLength;
                    continue;
                }

                // Normal entry
                var result = keyValueRegex.Match(l);

                if (result.Success)
                {
                    var key = result.Groups["key"].Value;
                    var value = result.Groups["value"] == null ? string.Empty : result.Groups["value"].Value;

                    m_Entries.Add(new KeyValuePair<string, string>(key, value));
                }
                else
                {
                    // Failed to parse value, add it as well for easier debugging
                    m_Entries.Add(new KeyValuePair<string, string>(FailedKey, l));
                }

                i++;
            }
        }

        private int GetBlockEnd(string[] lines, int blockStart)
        {
            // Figure out block end
            // For ex.,
            //  requested permissions:
            //      android.permission.INTERNET
            //      android.permission.ACCESS_NETWORK_STATE
            // if blockStart == 0, blockEnd will be 3
            var l = lines[blockStart];
            var indentation = l.TakeWhile(c => char.IsWhiteSpace(c)).Count();

            var blockEnd = blockStart + 1;
            while (blockEnd < lines.Length)
            {
                l = lines[blockEnd];
                var blockIndentation = l.TakeWhile(c => char.IsWhiteSpace(c)).Count();
                if (blockIndentation > indentation)
                    blockEnd++;
                else
                    break;
            }

            return blockEnd - 1;
        }

        private string ParsePermissionBlock(string[] lines, int permissionsStart, out int blockLength)
        {
            blockLength = 1;
            var value = string.Empty;

            if (permissionsStart + 1 >= lines.Length)
                return string.Empty;

            var permissionsEnd = GetBlockEnd(lines, permissionsStart);

            // Collect values
            for (int p = permissionsStart + 1; p <= permissionsEnd; p++)
            {
                if (value.Length > 0)
                    value += "\n";
                value += lines[p].Trim();
            }
            blockLength = permissionsEnd - permissionsStart + 1;
            return value;
        }

        internal string GetEntriesString()
        {
            var values = new StringBuilder();
            foreach (var e in Entries)
            {
                values.AppendLine($"{e.Key} = {e.Value}");
            }
            return values.ToString();
        }
    }
}
