using System;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using System.Collections.Generic;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatPackageInfoParser
    {
        // Note: Using List instead of Dictionary in case of duplicate keys
        List<KeyValuePair<string, string>> m_Entries;

        IReadOnlyList<KeyValuePair<string, string>> Entries => m_Entries;

        internal AndroidLogcatPackageInfoParser(string contents, string packageName)
        {
            m_Entries = new List<KeyValuePair<string, string>>();

            var lines = contents.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries).ToArray();

            if (lines.Length == 0)
                throw new System.Exception("No package info found");
            var regexPackageName = Regex.Escape(packageName);
            var title = new Regex($"Package.*{regexPackageName}.*\\:");

            if (!title.Match(lines[0]).Success)
                throw new Exception($"Expected 'Package [{packageName}] (<id>) :', but got '{lines[0]}'");

            var keyValueRegex = new Regex(@"\s+(?<key>\S+[^\=])\=(?<value>[^\=]\S+)");
            for (int i = 1; i < lines.Length; i++)
            {
                var l = lines[i];

                // Is it a permissions block
                if (l.EndsWith("permissions:"))
                {
                    var key = l.Trim();
                    var value = string.Empty;
                    m_Entries.Add(new KeyValuePair<string, string>(key, value));
                }
                else
                {
                    var result = keyValueRegex.Match(l);

                    if (result.Success)
                    {
                        var key = result.Groups["key"].Value;
                        var value = result.Groups["value"].Value;

                        m_Entries.Add(new KeyValuePair<string, string>(key, value));
                    }
                    else
                    {
                        // Keep failures too
                        m_Entries.Add(new KeyValuePair<string, string>("Failed", l));
                    }
                }

            }

        }

    }
}
