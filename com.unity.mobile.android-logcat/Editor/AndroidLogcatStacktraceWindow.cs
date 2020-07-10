using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatRegexList : AndroidLogcatReordableList
    {
        private AndroidLogcatRuntimeBase m_Runtime;
        public AndroidLogcatRegexList(List<ReordableListItem> dataSource, AndroidLogcatRuntimeBase runtime) : base(dataSource)
        {
            ShowResetGUI = true;
            m_Runtime = runtime;
        }

        protected override void OnResetButtonClicked()
        {
            m_Runtime.Settings.ResetStacktraceResolveRegex();
        }

        protected override string ValidateItem(string item)
        {
            if (string.IsNullOrEmpty(item))
                return string.Empty;

            try
            {
                Regex.Match("", item);
            }
            catch (ArgumentException ex)
            {
                return ex.Message;
            }

            return string.Empty;
        }
    }

    internal class AndroidLogcatSymbolList : AndroidLogcatReordableList
    {
        public AndroidLogcatSymbolList(List<ReordableListItem> dataSource) : base(dataSource)
        {
            ShowEntryGUI = false;
        }

        protected override void OnPlusButtonClicked()
        {
            var item = EditorUtility.OpenFolderPanel("Locate symbol path", CurrentItemName, "");
            if (string.IsNullOrEmpty(item))
                return;
            GUIUtility.keyboardControl = 0;
            AddItem(item);
        }

        protected override void DoListGUIWhenEmpty()
        {
            EditorGUILayout.HelpBox("Please add directories containing symbols for your native libraries.", MessageType.Info, true);
        }
    }

    internal class AndroidLogcatStacktraceWindow : EditorWindow
    {
#if PLATFORM_ANDROID
        static readonly string m_RedColor = "#ff0000ff";
        static readonly string m_GreenColor = "#00ff00ff";
        internal static readonly string m_DefaultAddressRegex = @"\s*#\d{2}\s*pc\s*(\S*)\s*.*(lib.*\.so)";

        enum WindowMode
        {
            OriginalLog,
            ResolvedLog
        }

        enum ToolbarMode
        {
            Regex,
            SymbolPaths
        }

        Vector2 m_ScrollPosition;
        string m_Text = String.Empty;
        string m_ResolvedStacktraces = String.Empty;

        private WindowMode m_WindowMode;
        private ToolbarMode m_ToolbarMode;

        private AndroidLogcatRuntimeBase m_Runtime;

        AndroidLogcatReordableList m_RegexList;
        AndroidLogcatReordableList m_SymbolPathList;

        public static void ShowStacktraceWindow()
        {
            var wnd = GetWindow<AndroidLogcatStacktraceWindow>();
            if (wnd == null)
                wnd = ScriptableObject.CreateInstance<AndroidLogcatStacktraceWindow>();
            wnd.titleContent = new GUIContent("Stacktrace Utility");
            wnd.Show();
            wnd.Focus();
        }

        internal static bool ParseLine(IReadOnlyList<ReordableListItem> regexs, string msg, out string address, out string libName)
        {
            foreach (var regexItem in regexs)
            {
                if (!regexItem.Enabled)
                    continue;

                var match = new Regex(regexItem.Name).Match(msg);
                if (match.Success)
                {
                    address = match.Groups["address"].Value;
                    libName = match.Groups["libName"].Value + ".so";
                    return true;
                }
            }

            address = null;
            libName = null;
            return false;
        }

        internal string GetSymbolFilePath(string libraryName)
        {
            foreach (var symbolPath in m_Runtime.ProjectSettings.SymbolPaths)
            {
                if (!symbolPath.Enabled)
                    continue;

                var file = AndroidLogcatUtilities.GetSymbolFile(symbolPath.Name, libraryName);
                if (!string.IsNullOrEmpty(file))
                    return file;
            }

            return string.Empty;
        }

        void ResolveStacktraces()
        {
            m_ResolvedStacktraces = String.Empty;
            if (string.IsNullOrEmpty(m_Text))
            {
                m_ResolvedStacktraces = string.Format(" <color={0}>(Please add some log with addresses first)</color>", m_RedColor);
                return;
            }

            var lines = m_Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var l in lines)
            {
                string address;
                string library;
                if (!ParseLine(m_Runtime.Settings.StacktraceResolveRegex, l, out address, out library))
                {
                    m_ResolvedStacktraces += l;
                }
                else
                {
                    string resolved = string.Format(" <color={0}>(Not resolved)</color>", m_RedColor);
                    var symbolFile = GetSymbolFilePath(library);
                    if (string.IsNullOrEmpty(symbolFile))
                    {
                        resolved = string.Format(" <color={0}>({1} not found)</color>", m_RedColor, library);
                    }
                    else
                    {
                        try
                        {
                            var result = AndroidLogcatManager.instance.Runtime.Tools.RunAddr2Line(symbolFile, new[] { address });
                            AndroidLogcatInternalLog.Log("addr2line \"{0}\" {1}", symbolFile, address);
                            if (!string.IsNullOrEmpty(result[0]))
                                resolved = string.Format(" <color={0}>({1})</color>", m_GreenColor, result[0].Trim());
                        }
                        catch (Exception ex)
                        {
                            m_ResolvedStacktraces = string.Format("Exception while running addr2line ('{0}', {1}):\n{2}", symbolFile, address, ex.Message);
                            return;
                        }
                    }

                    m_ResolvedStacktraces += l.Replace(address, address + resolved);
                }

                m_ResolvedStacktraces += Environment.NewLine;
            }
        }

        private void OnEnable()
        {
            m_Runtime = AndroidLogcatManager.instance.Runtime;
            if (string.IsNullOrEmpty(m_Text))
            {
                var placeholder = new StringBuilder();
                placeholder.AppendLine("Copy paste log with address and click Resolve Stackraces");
                placeholder.AppendLine("For example:");
                placeholder.AppendLine("2019-05-17 12:00:58.830 30759-30803/? E/CRASH: \t#00  pc 002983fc  /data/app/com.mygame==/lib/arm/libunity.so");
                m_Text = placeholder.ToString();
            }

            m_RegexList = new AndroidLogcatRegexList(m_Runtime.Settings.StacktraceResolveRegex, m_Runtime);
            m_SymbolPathList = new AndroidLogcatSymbolList(m_Runtime.ProjectSettings.SymbolPaths);
        }

        void DoInfoGUI()
        {
            const float kInfoAreaHeight = 200.0f;
            EditorGUILayout.BeginVertical(GUILayout.Height(kInfoAreaHeight));
            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);
            if (GUILayout.Toggle(m_ToolbarMode == ToolbarMode.Regex, "Configure Regex", AndroidLogcatStyles.toolbarButton))
                m_ToolbarMode = ToolbarMode.Regex;
            if (GUILayout.Toggle(m_ToolbarMode == ToolbarMode.SymbolPaths, "Configure Symbol Paths", AndroidLogcatStyles.toolbarButton))
                m_ToolbarMode = ToolbarMode.SymbolPaths;
            EditorGUILayout.EndHorizontal();

            switch (m_ToolbarMode)
            {
                case ToolbarMode.Regex:
                    m_RegexList.OnGUI();
                    break;
                case ToolbarMode.SymbolPaths:
                    m_SymbolPathList.OnGUI();
                    break;
            }

            EditorGUILayout.EndVertical();
        }

        void OnGUI()
        {
            DoInfoGUI();
            if (GUILayout.Button("Resolve"))
                ResolveStacktraces();

            EditorGUI.BeginChangeCheck();
            m_WindowMode = (WindowMode)GUILayout.Toolbar((int)m_WindowMode, new[] {new GUIContent("Original"), new GUIContent("Resolved"), }, "LargeButton", GUI.ToolbarButtonSize.FitToContents);
            if (EditorGUI.EndChangeCheck())
            {
                // Editor seems to be caching text from EditorGUILayout.TextArea
                // This invalidates the cache, and forces the text to change in text area
                GUIUtility.keyboardControl = 0;
                GUIUtility.hotControl = 0;
            }

            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);
            switch (m_WindowMode)
            {
                case WindowMode.ResolvedLog:
                    // Note: Not using EditorGUILayout.SelectableLabel, because scrollbars are not working correctly
                    EditorGUILayout.TextArea(m_ResolvedStacktraces, AndroidLogcatStyles.stacktraceStyle, GUILayout.ExpandHeight(true));
                    // Keep this commented, otherwise, it's not possible to select text in this text area and copy it.
                    //GUIUtility.keyboardControl = 0;
                    break;
                case WindowMode.OriginalLog:
                    m_Text = EditorGUILayout.TextArea(m_Text, AndroidLogcatStyles.stacktraceStyle, GUILayout.ExpandHeight(true));
                    break;
            }
            EditorGUILayout.EndScrollView();
        }

#else
        internal void OnGUI()
        {
#if !PLATFORM_ANDROID
            AndroidLogcatUtilities.ShowActivePlatformNotAndroidMessage();
#endif
        }

#endif
    }
}
