using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine.UIElements;

namespace Unity.Android.Logcat
{
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

        Vector2 m_ScrollPosition;
        string m_Text = String.Empty;
        string m_ResolvedStacktraces = String.Empty;

        private WindowMode m_WindowMode;

        private AndroidLogcatRuntimeBase m_Runtime;

        public static void ShowStacktraceWindow()
        {
            var wnd = GetWindow<AndroidLogcatStacktraceWindow>();
            if (wnd == null)
                wnd = ScriptableObject.CreateInstance<AndroidLogcatStacktraceWindow>();
            wnd.titleContent = new GUIContent("Stacktrace Utility");
            wnd.Show();
            wnd.Focus();
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

                if (!AndroidLogcatUtilities.ParseCrashLine(m_Runtime.Settings.StacktraceResolveRegex, l, out address, out library))
                {
                    m_ResolvedStacktraces += l;
                }
                else
                {
                    string resolved = string.Format(" <color={0}>(Not resolved)</color>", m_RedColor);
                    var symbolFile = AndroidLogcatUtilities.GetSymbolFile(m_Runtime.ProjectSettings.SymbolPaths, library);
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
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.unity.mobile.android-logcat/Editor/UI/Resources/StacktraceWindow.uxml");
            var child = tree.CloneTree();
            rootVisualElement.Add(child);

            child.style.flexGrow = 1;
            child.style.flexShrink = 0;
            child.Q<IMGUIContainer>("Operations").onGUIHandler = DoInfoGUI;
            child.Q<IMGUIContainer>("Toolbar").onGUIHandler = DoToolbarGUI;

            // var e = child.Q<EnumField>("Platform");
            //e.styleSheets

            //var iMGUIContainer = child.Q<IMGUIContainer>();
            // iMGUIContainer.onGUIHandler = DoGUI;

            /*
            var root = rootVisualElement;

            // Creates our button and sets its Text property.
            var field = new TextField() { maxLength = 0, multiline = true};

            field.SetValueWithoutNotify("sdsd");
            field.wi
            // Adds it to the root.
            root.Add(field);

            m_Runtime = AndroidLogcatManager.instance.Runtime;
            if (string.IsNullOrEmpty(m_Text))
            {
                var placeholder = new StringBuilder();
                placeholder.AppendLine("Copy paste log with address and click Resolve Stackraces");
                placeholder.AppendLine("For example:");
                placeholder.AppendLine("2019-05-17 12:00:58.830 30759-30803/? E/CRASH: \t#00  pc 002983fc  /data/app/com.mygame==/lib/arm/libunity.so");
                m_Text = placeholder.ToString();
            }
            */
        }

        void DoToolbarGUI()
        {
            EditorGUI.BeginChangeCheck();
            m_WindowMode = (WindowMode)GUILayout.Toolbar((int)m_WindowMode, new[] { new GUIContent("Original"), new GUIContent("Resolved"), }, "LargeButton", GUI.ToolbarButtonSize.Fixed, GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck())
            {
                // Editor seems to be caching text from EditorGUILayout.TextArea
                // This invalidates the cache, and forces the text to change in text area
                GUIUtility.keyboardControl = 0;
                GUIUtility.hotControl = 0;
            }

        }

        void DoInfoGUI()
        {
            EditorGUILayout.BeginVertical();

            // TODO: bug, after copy pasting to resolved text area and clicking this button, nothing happens
            if (GUILayout.Button("Resolve Stacktraces"))
            {
                GUIUtility.keyboardControl = 0;
                GUIUtility.hotControl = 0;
                ResolveStacktraces();
                m_WindowMode = WindowMode.ResolvedLog;
            }
            GUILayout.Space(20);
            if (GUILayout.Button("Open settings"))
                SettingsService.OpenUserPreferences(AndroidLogcatSettingsProvider.kSettingsPath);
            EditorGUILayout.EndVertical();
        }

        void OnGUI2()
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            EditorGUI.BeginChangeCheck();
            m_WindowMode = (WindowMode)GUILayout.Toolbar((int)m_WindowMode, new[] {new GUIContent("Original"), new GUIContent("Resolved"), }, "LargeButton", GUI.ToolbarButtonSize.Fixed, GUILayout.ExpandWidth(true));
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
            GUILayout.EndVertical();
            DoInfoGUI();
            GUILayout.EndHorizontal();
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
