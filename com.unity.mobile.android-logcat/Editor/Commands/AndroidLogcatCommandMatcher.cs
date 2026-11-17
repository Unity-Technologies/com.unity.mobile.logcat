using System;

namespace Unity.Android.Logcat
{
    internal static class AndroidLogcatCommandMatcher
    {
        internal static bool Matches(AndroidLogcatCommandEntry entry, string searchTerm)
        {
            if (entry == null)
                return false;

            if (string.IsNullOrWhiteSpace(searchTerm))
                return true;

            var term = searchTerm.Trim();
            return Contains(entry.name, term) || Contains(entry.command, term);
        }

        internal static bool Matches(AndroidLogcatCommandEntry entry, AndroidLogcatCommandCategory? category, string searchTerm)
        {
            if (entry == null)
                return false;

            if (category.HasValue && entry.category != category.Value)
                return false;

            return Matches(entry, searchTerm);
        }

        internal static bool MatchesUserCommand(AndroidLogcatCommandEntry entry, AndroidLogcatCommandCategory? category, string searchTerm)
        {
            if (entry == null)
                return false;

            if (category.HasValue &&
                entry.category != AndroidLogcatCommandCategory.Uncategorized &&
                entry.category != category.Value)
                return false;

            return Matches(entry, searchTerm);
        }

        static bool Contains(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack))
                return false;
            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

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
