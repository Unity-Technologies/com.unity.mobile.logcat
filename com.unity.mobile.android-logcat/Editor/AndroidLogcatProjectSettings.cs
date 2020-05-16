#if PLATFORM_ANDROID
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor.Android;
using System.Collections.Generic;
using System.Linq;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatProjectSettings
    {
        private static readonly string kAndroidLogcatSettingsPath = Path.Combine("ProjectSettings", "AndroidLogcatSettings.asset");

        public string m_SelectedDeviceId = String.Empty;

        public AndroidLogcatConsoleWindow.PackageInformation m_SelectedPackage = null;

        public AndroidLogcat.Priority m_SelectedPriority = AndroidLogcat.Priority.Verbose;

        public List<AndroidLogcatConsoleWindow.PackageInformation> m_PackagesForSerialization = null;

        public AndroidLogcatTagsControl m_TagControl = null;

        public string m_MemoryViewerJson;

        internal static AndroidLogcatProjectSettings Load()
        {
            if (!File.Exists(kAndroidLogcatSettingsPath))
                return null;

            var jsonString = File.ReadAllText(kAndroidLogcatSettingsPath);
            if (string.IsNullOrEmpty(jsonString))
                return null;

            try
            {
                var settings = new AndroidLogcatProjectSettings();
                JsonUtility.FromJsonOverwrite(jsonString, settings);
                return settings;
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log("Load Preferences from Json failed: " + ex.Message);
            }
            return null;
        }

        internal static void Save(AndroidLogcatProjectSettings settings)
        {
            var jsonString = JsonUtility.ToJson(settings, true);
            if (string.IsNullOrEmpty(jsonString))
                return;

            File.WriteAllText(kAndroidLogcatSettingsPath, jsonString);
        }
    }
}

#endif
