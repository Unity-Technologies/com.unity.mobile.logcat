using UnityEditor;
using UnityEngine;

namespace Unity.Android.Logcat
{
    class AndroidLogcatProjectSettingsProvider : SettingsProvider
    {
        internal static readonly string kSettingsPath = "Project/Analysis/Android Logcat Settings";
        class Styles
        {
            public static GUIContent symbolPaths = new GUIContent("Symbol Paths", "Configure symbol paths, used for resolving stack traces.");
        }

        private AndroidLogcatSymbolList m_SymbolList;

        public AndroidLogcatProjectSettingsProvider(string path, SettingsScope scope)
            : base(path, scope)
        {
            m_SymbolList = new AndroidLogcatSymbolList(AndroidLogcatManager.instance.Runtime.ProjectSettings.SymbolPaths);
        }

        public override void OnGUI(string searchContext)
        {
            EditorGUILayout.LabelField(Styles.symbolPaths, EditorStyles.boldLabel);
            m_SymbolList.OnGUI(150.0f);
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new AndroidLogcatProjectSettingsProvider(kSettingsPath, SettingsScope.Project);
            return provider;
        }
    }
}
