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

        // Only set on catalog entries; not saved, so exported JSON is unaffected.
        [NonSerialized] internal string description;

        internal AndroidLogcatCommandEntry() { }

        internal AndroidLogcatCommandEntry(string name, string command,
                                           AndroidLogcatCommandCategory category = AndroidLogcatCommandCategory.Uncategorized,
                                           string description = null)
        {
            this.name = name;
            this.command = command;
            this.category = category;
            this.description = description;
        }

        internal bool IsValid => !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(command);

        internal AndroidLogcatCommandEntry Clone() => new AndroidLogcatCommandEntry(name, command, category, description);
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
