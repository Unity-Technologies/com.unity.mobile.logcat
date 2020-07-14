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
    class AndroidLogcatSettingsProvider : SettingsProvider
    {
        internal static readonly string kSettingsPath = "Preferences/Analysis/Android Logcat Settings";

        class Styles
        {
            public static GUIContent maxMessageCount = new GUIContent("Max Count", "The maximum number of messages.");
            public static GUIContent font = new GUIContent("Font", "Font used for displaying messages");
            public static GUIContent fontSize = new GUIContent("Font Size");
            public static GUIContent stactrace = new GUIContent("Stack trace");
            public static GUIContent configureRegex = new GUIContent("Configure Regex", @"Global setting, shared by all projects, used for resolving library name and address name");
            public static GUIContent configureSymbolPaths = new GUIContent("Configure Symbol Paths", @"Per project setting, used for locating library's native symbol file, symbol file is used for demangling native function address into native function name");
        }

        private const string kStacktraceToolbar = "LogcatStacktraceToolbar";
        private const int kStacktraceToolbarRegex = 0;
        private const int kStacktraceToolbarSymbolPaths = 1;
        private AndroidLogcatRuntimeBase m_Runtime;
        private AndroidLogcatRegexList m_RegexList;
        private AndroidLogcatSymbolList m_SymbolList;

        private AndroidLogcatSettings Settings => m_Runtime.Settings;
        private AndroidLogcatProjectSettings ProjectSettings => m_Runtime.ProjectSettings;

        private int StacktraceToolbar
        {
            get => SessionState.GetInt(kStacktraceToolbar, kStacktraceToolbarRegex);
            set => SessionState.SetInt(kStacktraceToolbar, value);
        }

        public AndroidLogcatSettingsProvider(string path, SettingsScope scope)
            : base(path, scope)
        {
            m_Runtime = AndroidLogcatManager.instance.Runtime;
            m_RegexList = new AndroidLogcatRegexList(Settings.StacktraceResolveRegex, m_Runtime);
            m_SymbolList = new AndroidLogcatSymbolList(ProjectSettings.SymbolPaths);
        }

        public override void OnGUI(string searchContext)
        {
            var settings = Settings;
            EditorGUILayout.LabelField("Messages", EditorStyles.boldLabel);
            settings.MaxMessageCount = EditorGUILayout.IntSlider(Styles.maxMessageCount, settings.MaxMessageCount, 1, 100000);
            settings.MessageFont = (Font)EditorGUILayout.ObjectField(Styles.font, settings.MessageFont, typeof(Font), true);
            settings.MessageFontSize = EditorGUILayout.IntSlider(Styles.fontSize, settings.MessageFontSize, 5, 25);

            GUILayout.Space(20);
            EditorGUILayout.LabelField("Message Colors", EditorStyles.boldLabel);
            foreach (var p in (AndroidLogcat.Priority[])Enum.GetValues(typeof(AndroidLogcat.Priority)))
            {
                settings.SetMessageColor(p, EditorGUILayout.ColorField(p.ToString(), settings.GetMessageColor(p)));
            }
            GUILayout.Space(20);
            EditorGUILayout.LabelField("Memory Window", EditorStyles.boldLabel);
            settings.MemoryRequestIntervalMS = EditorGUILayout.IntField("Request Interval ms", settings.MemoryRequestIntervalMS);
            GUILayout.Space(20);
            DoStacktraceGUI();


            GUILayout.Space(20);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset"))
                settings.Reset();
            GUILayout.Space(5);
            GUILayout.EndHorizontal();
        }

        void DoStacktraceGUI()
        {
            EditorGUILayout.LabelField(Styles.stactrace, EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);

            if (GUILayout.Toggle(StacktraceToolbar == kStacktraceToolbarRegex, Styles.configureRegex, AndroidLogcatStyles.toolbarButton))
                StacktraceToolbar =  kStacktraceToolbarRegex;
            if (GUILayout.Toggle(StacktraceToolbar == kStacktraceToolbarSymbolPaths, Styles.configureSymbolPaths, AndroidLogcatStyles.toolbarButton))
                StacktraceToolbar = kStacktraceToolbarSymbolPaths;
            EditorGUILayout.EndHorizontal();

            float height = 400.0f;
            switch (StacktraceToolbar)
            {
                case kStacktraceToolbarRegex:
                    m_RegexList.OnGUI(height);
                    break;
                case kStacktraceToolbarSymbolPaths:
                    m_SymbolList.OnGUI(height);
                    break;
            }
        }

        [SettingsProvider]
        public static SettingsProvider CreateMyCustomSettingsProvider()
        {
            var provider = new AndroidLogcatSettingsProvider(kSettingsPath, SettingsScope.User);
            return provider;
        }
    }
}
#endif
