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

        [SerializeField]
        private Font m_MessageFont;

        [SerializeField]
        private int m_MessageFontSize;

        [SerializeField]
        private List<Color> m_MessageColors;

        internal int MaxMessageCount
        {
            set
            {
                if (m_MaxMessageCount == value)
                    return;
                m_MaxMessageCount = value;
                InvokeOnSettingsChanged();
            }
            get
            {
                return m_MaxMessageCount;
            }
        }

        internal Font MessageFont
        {
            set
            {
                if (m_MessageFont == value)
                    return;
                m_MessageFont = value;
                InvokeOnSettingsChanged();
            }
            get
            {
                return m_MessageFont;
            }
        }

        internal int MessageFontSize
        {
            set
            {
                if (m_MessageFontSize == value)
                    return;
                m_MessageFontSize = value;
                InvokeOnSettingsChanged();
            }
            get
            {
                return m_MessageFontSize;
            }
        }

        internal void SetMessageColor(AndroidLogcat.Priority priority, Color color)
        {
            // Populate the color list if needed
            while ((int)priority >= m_MessageColors.Count)
                m_MessageColors.Add(Color.white);

            if (m_MessageColors[(int)priority] == color)
                return;
            m_MessageColors[(int)priority] = color;
            InvokeOnSettingsChanged();
        }

        internal Color GetMessageColor(AndroidLogcat.Priority priority)
        {
            if ((int)priority < m_MessageColors.Count)
                return m_MessageColors[(int)priority];
            return Color.white;
        }

        internal Action<AndroidLogcatSettings> OnSettingsChanged;

        private AndroidLogcatSettings()
        {
            Reset();
        }

        internal void Reset()
        {
            m_MaxMessageCount = 60000;
            m_MessageFont = (Font)EditorGUIUtility.LoadRequired(UnityEditor.Experimental.EditorResources.fontsPath + "consola.ttf");
            m_MessageFontSize = 11;
            if (Enum.GetValues(typeof(AndroidLogcat.Priority)).Length != 6)
                throw new Exception("Unexpected length of Priority enum.");

            m_MessageColors = new List<Color>(new[]
            {
                Color.white,
                Color.white,
                Color.white,
                Color.yellow,
                Color.red,
                Color.red
            });
            InvokeOnSettingsChanged();
        }

        private void InvokeOnSettingsChanged()
        {
            if (OnSettingsChanged == null)
                return;
            OnSettingsChanged(this);
        }

        internal static AndroidLogcatSettings Load()
        {
            var settings = new AndroidLogcatSettings();

            var data = EditorPrefs.GetString(kSettingsName, "");
            if (string.IsNullOrEmpty(data))
                return settings;

            try
            {
                EditorJsonUtility.FromJsonOverwrite(data, settings);
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
                throw new NullReferenceException("Android logcat settings value was null");

            var data = EditorJsonUtility.ToJson(settings);
            EditorPrefs.SetString(kSettingsName, data);
        }
    }

    class AndroidLogcatSettingsProvider : SettingsProvider
    {
        class Styles
        {
            public static GUIContent maxMessageCount = new GUIContent("Max Count", "The maximum number of messages.");
            public static GUIContent font = new GUIContent("Font", "Font used for displaying messages");
            public static GUIContent fontSize = new GUIContent("Font Size");
        }

        public AndroidLogcatSettingsProvider(string path, SettingsScope scope)
            : base(path, scope) {}

        public override void OnGUI(string searchContext)
        {
            var settings = AndroidLogcatManager.instance.Runtime.Settings;
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
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset"))
                settings.Reset();
            GUILayout.Space(5);
            GUILayout.EndHorizontal();
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
