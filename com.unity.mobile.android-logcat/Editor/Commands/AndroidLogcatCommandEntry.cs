using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Android.Logcat
{
    internal enum AndroidLogcatCommandCategory
    {
        Uncategorized = 0,
        DeviceManagement,
        Packages,
        Permissions,
        FileTransfer,
        SystemInfo,
        Dumpsys,
        Settings,
        Networking,
        Advanced,
        Quest,
        Bundletool
    }

    [Serializable]
    internal class AndroidLogcatCommandEntry
    {
        [SerializeField] internal string name;
        [SerializeField] internal string command;
        [SerializeField] internal AndroidLogcatCommandCategory category;

        internal AndroidLogcatCommandEntry() { }

        internal AndroidLogcatCommandEntry(string name, string command,
                                           AndroidLogcatCommandCategory category = AndroidLogcatCommandCategory.Uncategorized)
        {
            this.name = name;
            this.command = command;
            this.category = category;
        }

        internal bool IsValid => !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(command);

        internal AndroidLogcatCommandEntry Clone() => new AndroidLogcatCommandEntry(name, command, category);
    }

    [Serializable]
    internal class AndroidLogcatCommandExportData
    {
        public AndroidLogcatCommandEntry[] favorites = Array.Empty<AndroidLogcatCommandEntry>();
        public AndroidLogcatCommandEntry[] general = Array.Empty<AndroidLogcatCommandEntry>();
    }

    internal static class AndroidLogcatCommandImport
    {
        internal static List<AndroidLogcatCommandEntry> Sanitize(IEnumerable<AndroidLogcatCommandEntry> entries)
        {
            var result = new List<AndroidLogcatCommandEntry>();
            if (entries == null)
                return result;

            foreach (var entry in entries)
            {
                if (entry == null || !entry.IsValid)
                    continue;
                result.Add(new AndroidLogcatCommandEntry(entry.name.Trim(), entry.command.Trim(), entry.category));
            }
            return result;
        }
    }
}
