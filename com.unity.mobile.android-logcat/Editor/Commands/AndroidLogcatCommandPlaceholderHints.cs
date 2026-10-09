using System;
using System.Collections.Generic;

namespace Unity.Android.Logcat
{
    internal static class AndroidLogcatCommandPlaceholderHints
    {
        internal class Hint
        {
            internal string Example { get; }

            internal string Description { get; }

            internal string[] Suggestions { get; }

            internal Hint(string example, string description, string[] suggestions = null)
            {
                Example = example ?? string.Empty;
                Description = description ?? string.Empty;
                Suggestions = suggestions ?? Array.Empty<string>();
            }
        }

        internal static readonly string[] CommonPermissions =
        {
            "android.permission.CAMERA",
            "android.permission.RECORD_AUDIO",
            "android.permission.ACCESS_FINE_LOCATION",
            "android.permission.ACCESS_COARSE_LOCATION",
            "android.permission.POST_NOTIFICATIONS",
            "android.permission.READ_EXTERNAL_STORAGE",
            "android.permission.WRITE_EXTERNAL_STORAGE",
            "android.permission.READ_MEDIA_IMAGES",
            "android.permission.READ_MEDIA_VIDEO",
            "android.permission.READ_MEDIA_AUDIO",
            "android.permission.BLUETOOTH_CONNECT",
            "android.permission.BLUETOOTH_SCAN",
            "android.permission.READ_PHONE_STATE",
            "android.permission.VIBRATE",
            "android.permission.INTERNET"
        };

        static readonly string[] kQuestLevels = { "0", "1", "2", "3", "4" };
        static readonly string[] kQuestRefreshRates = { "72", "90", "120" };

        static readonly Dictionary<string, Hint> s_Hints = new Dictionary<string, Hint>(StringComparer.OrdinalIgnoreCase)
        {
            ["package"] = new Hint("com.mycompany.myapp",
                "Application identifier. Prefilled from Player Settings."),
            ["package/activity"] = new Hint("com.mycompany.myapp/com.unity3d.player.UnityPlayerActivity",
                "Package and activity, separated by '/'. Prefilled from Player Settings."),

            ["permission"] = new Hint("android.permission.CAMERA",
                "Fully qualified permission name, including the 'android.permission.' prefix.",
                CommonPermissions),

            ["local"] = new Hint("Temp/data.bin",
                "Path on your computer. Relative paths resolve against the Editor's working directory."),
            ["local_path"] = new Hint("Temp/tombstone_00",
                "Path on your computer. Relative paths resolve against the Editor's working directory."),
            ["local.obb"] = new Hint("Temp/main.1.com.mycompany.myapp.obb",
                "The .obb file on your computer."),
            ["path.apk"] = new Hint("Builds/myapp.apk", "The .apk file on your computer."),
            ["path1.apk"] = new Hint("Builds/base-master.apk", "First split .apk on your computer."),
            ["path2.apk"] = new Hint("Builds/split_config.arm64_v8a.apk", "Second split .apk on your computer."),
            ["path.aab"] = new Hint("Builds/myapp.aab", "The Android App Bundle on your computer."),
            ["path.apks"] = new Hint("Builds/myapp.apks", "An .apks archive produced by bundletool build-apks."),
            ["output.apks"] = new Hint("Builds/myapp.apks", "Where bundletool should write the .apks archive."),
            ["path-to-bundletool.jar"] = new Hint("C:/Android/bundletool-all.jar",
                "bundletool jar. Prefer an absolute path - non adb commands resolve relative to the Editor's working directory."),
            ["device-spec.json"] = new Hint("Temp/device-spec.json",
                "Where bundletool should write the device spec."),

            ["remote"] = new Hint("/sdcard/Download/data.bin", "Path on the device."),
            ["path"] = new Hint("/sdcard/Download", "Path on the device."),
            ["tombstone_XX"] = new Hint("tombstone_00",
                "Tombstone file name. 'List Directory' on /data/tombstones shows what's available."),

            ["ip"] = new Hint("192.168.1.42", "The device's IP address on your local network."),
            ["local-port"] = new Hint("8080", "Port number on your computer."),
            ["remote-port"] = new Hint("8080", "Port number on the device."),
            ["host"] = new Hint("8.8.8.8", "Host name or IP address to reach from the device."),

            ["ms"] = new Hint("60000", "Duration in milliseconds."),

            ["0-4"] = new Hint("2", "Level from 0 (lowest) to 4 (highest).", kQuestLevels),
            ["72|90|120"] = new Hint("90", "Display refresh rate in Hz, as supported by the headset.", kQuestRefreshRates)
        };

        internal static Hint Get(string token)
        {
            if (string.IsNullOrEmpty(token))
                return null;
            return s_Hints.TryGetValue(token, out var hint) ? hint : null;
        }

        internal static string GetExample(string token) => Get(token)?.Example ?? string.Empty;

        internal static string GetDescription(string token) => Get(token)?.Description ?? string.Empty;

        internal static string[] GetSuggestions(string token) => Get(token)?.Suggestions ?? Array.Empty<string>();
    }
}
