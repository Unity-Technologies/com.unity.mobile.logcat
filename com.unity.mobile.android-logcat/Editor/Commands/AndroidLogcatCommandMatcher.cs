using System;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// Search matching for command entries. Deliberately free of any UI dependency so the behaviour
    /// can be unit tested, and null safe because entries may originate from user supplied JSON.
    /// </summary>
    internal static class AndroidLogcatCommandMatcher
    {
        /// <summary>
        /// Case insensitive substring match against the entry name and command.
        /// An empty search term matches everything; a null entry matches nothing.
        /// </summary>
        internal static bool Matches(AndroidLogcatCommandEntry entry, string searchTerm)
        {
            if (entry == null)
                return false;

            if (string.IsNullOrWhiteSpace(searchTerm))
                return true;

            var term = searchTerm.Trim();
            return Contains(entry.name, term) || Contains(entry.command, term);
        }

        /// <summary>
        /// Matches an entry against both a category filter and a search term.
        /// Pass null for <paramref name="category"/> to skip category filtering.
        /// </summary>
        internal static bool Matches(AndroidLogcatCommandEntry entry, AndroidLogcatCommandCategory? category, string searchTerm)
        {
            if (entry == null)
                return false;

            if (category.HasValue && entry.category != category.Value)
                return false;

            return Matches(entry, searchTerm);
        }

        static bool Contains(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack))
                return false;
            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Human readable label for a category, used for the filter chips.
        /// </summary>
        internal static string GetCategoryDisplayName(AndroidLogcatCommandCategory category)
        {
            switch (category)
            {
                case AndroidLogcatCommandCategory.DeviceManagement: return "Devices";
                case AndroidLogcatCommandCategory.Packages: return "Packages";
                case AndroidLogcatCommandCategory.Permissions: return "Permissions";
                case AndroidLogcatCommandCategory.FileTransfer: return "Files";
                case AndroidLogcatCommandCategory.SystemInfo: return "System Info";
                case AndroidLogcatCommandCategory.Dumpsys: return "Dumpsys";
                case AndroidLogcatCommandCategory.Settings: return "Settings";
                case AndroidLogcatCommandCategory.Networking: return "Network";
                case AndroidLogcatCommandCategory.Advanced: return "Advanced";
                case AndroidLogcatCommandCategory.Quest: return "Quest";
                case AndroidLogcatCommandCategory.Bundletool: return "Bundletool";
                default: return "Other";
            }
        }
    }
}
