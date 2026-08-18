using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// Groups catalog commands so the search window can offer category filters.
    /// Note: categories which duplicate functionality already provided by this package
    /// (logcat streaming, input simulation, screen capture) are deliberately absent -
    /// use the dedicated windows for those instead.
    /// </summary>
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

        /// <summary>
        /// An entry is only usable if it has both a name and a command. Entries deserialized from
        /// user supplied JSON may be missing either, which would throw further down in the UI.
        /// </summary>
        internal bool IsValid => !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(command);

        internal AndroidLogcatCommandEntry Clone() => new AndroidLogcatCommandEntry(name, command, category);
    }

    [Serializable]
    internal class AndroidLogcatCommandExportData
    {
        public AndroidLogcatCommandEntry[] favorites = Array.Empty<AndroidLogcatCommandEntry>();
        public AndroidLogcatCommandEntry[] general = Array.Empty<AndroidLogcatCommandEntry>();
    }

    /// <summary>
    /// Pure helpers for sanitizing imported command data. Kept free of UI so they can be unit tested.
    /// </summary>
    internal static class AndroidLogcatCommandImport
    {
        /// <summary>
        /// Drops null and incomplete entries, trimming the survivors.
        /// </summary>
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
