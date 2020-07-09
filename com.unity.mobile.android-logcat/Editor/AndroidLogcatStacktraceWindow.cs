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
        public AndroidLogcatRegexList(List<ReordableListItem> dataSource) : base(dataSource) { }

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
// todo fix
        internal static readonly string m_AddressRegexFormat1 = @"\s*#\d{2}\s*pc\s(?<address>[a-fA-F0-9]{8}).*(?<libName>lib.*)\.so";
        internal static readonly string m_AddressRegexFormat2 = @"\s*at (?<libName>lib.*)\.(?<address>[a-fA-F0-9]{8})";

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

        internal static bool ParseLine(Regex regex, string msg, out string address, out string libName)
        {
            var match = regex.Match(msg);
            if (match.Success)
            {
                address = match.Groups["address"].Value;
                libName = match.Groups["libName"].Value + ".so";
                return true;
            }
            address = null;
            libName = null;
            return false;
        }

        static string ConvertSlashToUnicodeSlash(string text_)
        {
            return text_.Replace("/", " \u2215");
        }

        void ResolveStacktraces(string symbolPath, Regex regex)
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
                if (!ParseLine(regex, l, out address, out library))
                {
                    m_ResolvedStacktraces += l;
                }
                else
                {
                    string resolved = string.Format(" <color={0}>(Not resolved)</color>", m_RedColor);
                    var symbolFile = AndroidLogcatUtilities.GetSymbolFile(symbolPath, library);
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
            
            m_RegexList = new AndroidLogcatRegexList(m_Runtime.Settings.StacktraceResolveRegex);
            m_SymbolPathList = new AndroidLogcatSymbolList(m_Runtime.ProjectSettings.SymbolPaths);
        }

        void DoRegex(float labelWidth, Regex regex)
        {
            /*
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
//TODO fix
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Address regex:", EditorStyles.boldLabel, GUILayout.Width(labelWidth));
            m_AddressRegex = GUILayout.TextField(m_AddressRegex);
            EditorGUILayout.EndHorizontal();
            Regex regex;
            try
            {
                regex = new Regex(m_AddressRegex);
            }
            catch (Exception ex)
            {
                var oldColor = GUI.color;
                GUI.color = Color.red;
                GUILayout.Label(ex.GetType().Name + " : " + ex.Message, AndroidLogcatStyles.errorStyle);
                regex = null;
                GUI.color = oldColor;
            }
            EditorGUILayout.EndVertical();

            var regs = new[] { m_AddressRegexFormat1, m_AddressRegexFormat2, m_CustomAddressRegex };
            if (m_SelectedRegex > regs.Length - 1)
                m_SelectedRegex = 0;

            GUILayout.Label("Select regex for address/library name resolving:", EditorStyles.boldLabel);
            for (int i = 0; i < regs.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                bool value = GUILayout.Toggle(m_SelectedRegex == i, "", GUILayout.ExpandWidth(false));
                if (value)
                    m_SelectedRegex = i;
                var isPredefinedRegex = i < regs.Length - 1;
                GUILayout.Label(isPredefinedRegex ? "(Predefined)" : "(Custom)", GUILayout.Width(100));
                if (isPredefinedRegex)
                    GUILayout.Label(regs[i], EditorStyles.textField, GUILayout.ExpandWidth(true));
                else
                    regs[i] = m_CustomAddressRegex = GUILayout.TextField(m_CustomAddressRegex, GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical();
            EditorGUI.BeginDisabledGroup(m_SelectedSymbolPath < 0);
            if (GUILayout.Button("Resolve Stacktraces", EditorStyles.miniButton) && regex != null)
            {
                m_WindowMode = WindowMode.ResolvedLog;
                ResolveStacktraces(m_RecentSymbolPaths[m_SelectedSymbolPath], new Regex(regs[m_SelectedRegex]));
                GUIUtility.keyboardControl = 0;
                GUIUtility.hotControl = 0;
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
            */
        }

        void OnGUI()
        {
            const float kLabelWidth = 120.0f;
            const float kInfoAreaHeight = 200.0f;
            EditorGUILayout.BeginVertical(GUILayout.Height(kInfoAreaHeight));
            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);
            if (GUILayout.Toggle(m_ToolbarMode == ToolbarMode.Regex, "Configure Regex", AndroidLogcatStyles.toolbarButton))
                m_ToolbarMode = ToolbarMode.Regex;
            if (GUILayout.Toggle(m_ToolbarMode == ToolbarMode.SymbolPaths, "Configure Symbol Paths", AndroidLogcatStyles.toolbarButton))
                m_ToolbarMode = ToolbarMode.SymbolPaths;
            EditorGUILayout.EndHorizontal();

            // GUILayout.Box("", AndroidLogcatStyles.columnHeader, GUILayout.Width(position.width), GUILayout.Height(kInfoAreaHeight));
            // GUILayout.BeginArea(new Rect(0, 0, this.position.width, kInfoAreaHeight));

            switch (m_ToolbarMode)
            {
                case ToolbarMode.Regex:
                    m_RegexList.OnGUI();
                    break;
                case ToolbarMode.SymbolPaths:
                    m_SymbolPathList.OnGUI();
                    break;
            }

            if (GUILayout.Button("Test"))
            {
                //  PopupWindow.Show(GUILayoutUtility.GetLastRect(), new AndroidLogcatReordableList(
                //       new List<ReordableListItem>(new[] { new ReordableListItem() { Name = "sds", Enabled = true } })));
            }
            EditorGUILayout.EndVertical();
            DoRegex(kLabelWidth, null);
            //  GUILayout.EndArea();


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
