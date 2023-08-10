using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatPackageInfoParser
    {
        internal static readonly string FailedKey = "Failed";
        private string m_Contents;

        internal AndroidLogcatPackageInfoParser(string contents)
        {
            m_Contents = contents.Replace("\r\n", "\n");
        }

        public List<string> ParsePackageInformationAsSingleEntries(string packageName)
        {
            var lines = GetPackageBlock(packageName);
            if (lines == null || lines.Length <= 1)
                return new List<string>();
            var strip = lines[1].TakeWhile(c => char.IsWhiteSpace(c)).Count();

            var entries = lines.Select(c => c.Substring(strip)).ToList();
            entries.RemoveAt(0);
            return entries;
        }

        public List<string> ParseLaunchableActivities(string packageName)
        {
            var lines = GetActivitiesBlock();
            var activityResolveRegex = new Regex(Regex.Escape($"{packageName}/") + "(?<activityName>\\S+) filter");
            if (lines == null || lines.Length <= 1)
                return new List<string>();

            var activities = new List<string>();
            foreach (var line in lines)
            {
                var result = activityResolveRegex.Match(line);
                if (!result.Success)
                    continue;
                activities.Add(result.Groups["activityName"].Value);
            }
            return activities;
        }

        /// <summary>
        /// Parses information as [key]=[value]
        /// In some cases like permissions or User, it's difficult to present such data
        /// For ex.,
        ///     requested permissions:
        ///        android.permission.INTERNET
        ///        android.permission.ACCESS_NETWORK_STATE
        ///     User 0: ceDataInode=311793 installed=true hidden=false suspended=false distractionFlags=0 stopped=false notLaunched=false enabled=0 instant=false virtual=false
        ///        gids=[3003]
        ///        runtime permissions:
        /// </summary>
        public List<KeyValuePair<string, string>> ParsePackageInformationAsPairs(string packageName)
        {
            var lines = GetPackageBlock(packageName);
            if (lines == null)
                return new List<KeyValuePair<string, string>>();

            return ParsePackageInformationAsPairs(lines, packageName);
        }

        private List<KeyValuePair<string, string>> ParsePackageInformationAsPairs(string[] lines, string packageName)
        {
            var entries = new List<KeyValuePair<string, string>>();
            if (lines.Length == 0)
                return entries;

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
                    entries.Add(new KeyValuePair<string, string>(key, values));
                    i += blockLength;
                    continue;
                }

                // Normal entry
                var result = keyValueRegex.Match(l);

                if (result.Success)
                {
                    var key = result.Groups["key"].Value;
                    var value = result.Groups["value"] == null ? string.Empty : result.Groups["value"].Value;

                    entries.Add(new KeyValuePair<string, string>(key, value));
                }
                else
                {
                    // Failed to parse value, add it as well for easier debugging
                    entries.Add(new KeyValuePair<string, string>(FailedKey, l));
                }

                i++;
            }
            return entries;
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
                    value += ", ";
                value += lines[p].Trim();
            }
            blockLength = permissionsEnd - permissionsStart + 1;
            return value;
        }

        private string[] GetActivitiesBlock()
        {
            return GetBlock($"Activity Resolver Table:");
        }

        private string[] GetPackageBlock(string packageName)
        {
            return GetBlock($"Package [{packageName}]");
        }

        private string[] GetBlock(string blockStartName)
        {
            var lines = m_Contents.Split(new[] { '\n' }).ToArray();
            for (int i = 0; i < lines.Length; i++)
            {
                // Find block start
                if (!lines[i].Trim().StartsWith(blockStartName))
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
                    return Array.Empty<string>();

                return lines.Skip(i).Take(length).ToArray();
            }

            return Array.Empty<string>();
        }
    }
}
