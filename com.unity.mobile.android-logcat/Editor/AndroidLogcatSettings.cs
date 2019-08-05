#if PLATFORM_ANDROID
using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.Text.RegularExpressions;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEditor.Android;
using System.Text;
using UnityEngine.UIElements;

namespace Unity.Android.Logcat
{
    [Serializable]
    internal class AndroidLogcatSettings
    {
        internal static string kSettingsName = "AndroidLogcatSettings";

        [SerializeField]
        private int m_MaxMessageCount;

        internal int MaxMessageCount { set { m_MaxMessageCount = value; } get { return m_MaxMessageCount; } }

        private AndroidLogcatSettings()
        {
            m_MaxMessageCount = 60000;
        }

        internal static AndroidLogcatSettings Load()
        {
            var settings = new AndroidLogcatSettings();

            var data = EditorPrefs.GetString(kSettingsName, "");
            if (string.IsNullOrEmpty(data))
                return settings;

            try
            {
                settings = JsonUtility.FromJson<AndroidLogcatSettings>(data);
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log("Load Android Logcat Settings from Json failed: " + ex.Message);
            }
            return settings;
        }

        internal static void Save(AndroidLogcatSettings settings)
        {
            if (settings == null)
                throw new NullReferenceException("settings");

            var data = JsonUtility.ToJson(settings);
            EditorPrefs.SetString(kSettingsName, data);
        }
    }

    class AndroidLogcatSettingsProvider : SettingsProvider
    {
        class Styles
        {
            public static GUIContent maxMessageCount = new GUIContent("Max Message Count", "The maximum number of messages.");
        }

        public AndroidLogcatSettingsProvider(string path, SettingsScope scope)
            : base(path, scope) {}

        public override void OnGUI(string searchContext)
        {
            var settings = AndroidLogcatManager.instance.Runtime.Settings;
            settings.MaxMessageCount = EditorGUILayout.IntSlider(Styles.maxMessageCount, settings.MaxMessageCount, 1, 100000);
        }

        [SettingsProvider]
        public static SettingsProvider CreateMyCustomSettingsProvider()
        {
            var provider = new AndroidLogcatSettingsProvider("Preferences/Android Logcat Settings", SettingsScope.User);
            return provider;
        }
    }
}
#endif
