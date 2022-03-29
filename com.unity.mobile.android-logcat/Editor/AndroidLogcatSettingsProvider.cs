using System;
using UnityEditor;
using UnityEngine;

namespace Unity.Android.Logcat
{
    class AndroidLogcatSettingsProvider : SettingsProvider
    {
        internal static readonly string kSettingsPath = "Preferences/Analysis/Android Logcat Settings";

        class Styles
        {
            public static GUIContent maxUnfilteredMessageCount = new GUIContent("Max Unfiltered Messages", "The maximum number of unfiltered messages which are kept in the cache and are used when filtering messages. The number of unfiltered messages affects the performing of filtering. 0 - no limit.");
            public static GUIContent maxFilteredMessageCount = new GUIContent("Max Filtered Messages", "The maximum number of filtered messages which are shown in the list, cannot be bigger than unfiltered message count. 0 - no limit.");
            public static GUIContent font = new GUIContent("Font", "Font used for displaying messages");
            public static GUIContent fontSize = new GUIContent("Font Size");
            public static GUIContent stactraceRegex = new GUIContent("Stacktrace Regex", "Configure regex used for resolving function address and library name");
            public static GUIContent requestIntervalMS = new GUIContent("Request Interval ms",
                $"How often to request memory dump from the device? The minimum value is {AndroidLogcatSettings.kMinMemoryRequestIntervalMS} ms");
            public static GUIContent maxExitedPackageToShow = new GUIContent("Max Exited Packages", "The maximum number of packages in package selection which have exited.");
        }

        private AndroidLogcatRuntimeBase m_Runtime;
        private AndroidLogcatRegexList m_RegexList;

        private AndroidLogcatSettings Settings => m_Runtime.Settings;


        public AndroidLogcatSettingsProvider(string path, SettingsScope scope)
            : base(path, scope)
        {
            m_Runtime = AndroidLogcatManager.instance.Runtime;
            m_RegexList = new AndroidLogcatRegexList(Settings.StacktraceResolveRegex, m_Runtime);
        }

        public override void OnGUI(string searchContext)
        {
            var settings = Settings;
            EditorGUILayout.LabelField("Messages", EditorStyles.boldLabel);
            settings.MaxUnfilteredMessageCount = EditorGUILayout.IntSlider(Styles.maxUnfilteredMessageCount, settings.MaxUnfilteredMessageCount, 0, 100000);
            settings.MaxFilteredMessageCount = EditorGUILayout.IntSlider(Styles.maxFilteredMessageCount, settings.MaxFilteredMessageCount, 0, 100000);

            settings.MessageFont = (Font)EditorGUILayout.ObjectField(Styles.font, settings.MessageFont, typeof(Font), true);
            settings.MessageFontSize = EditorGUILayout.IntSlider(Styles.fontSize, settings.MessageFontSize, 5, 25);

            GUILayout.Space(20);
            EditorGUILayout.LabelField("Message Colors", EditorStyles.boldLabel);
            foreach (var p in (Priority[])Enum.GetValues(typeof(Priority)))
            {
                settings.SetMessageColor(p, EditorGUILayout.ColorField(p.ToString(), settings.GetMessageColor(p)));
            }

            GUILayout.Space(20);
            EditorGUILayout.LabelField("Memory Window", EditorStyles.boldLabel);
            settings.MemoryRequestIntervalMS =
                EditorGUILayout.IntField(Styles.requestIntervalMS, settings.MemoryRequestIntervalMS);
            GUILayout.Space(20);

            GUILayout.Space(20);
            EditorGUILayout.LabelField("Packages", EditorStyles.boldLabel);

            settings.MaxExitedPackagesToShow = EditorGUILayout.IntSlider(Styles.maxExitedPackageToShow, settings.MaxExitedPackagesToShow, 1, 100);

            GUILayout.Space(20);
            EditorGUILayout.LabelField(Styles.stactraceRegex, EditorStyles.boldLabel);
            m_RegexList.OnGUI(150.0f);

            GUILayout.Space(20);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset"))
                settings.Reset();
            GUILayout.Space(5);
            GUILayout.EndHorizontal();
        }

        [SettingsProvider]
        public static SettingsProvider CreateAndroidLogcatSettingsProvider()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
                return null;
            var provider = new AndroidLogcatSettingsProvider(kSettingsPath, SettingsScope.User);
            return provider;
        }
    }
}
