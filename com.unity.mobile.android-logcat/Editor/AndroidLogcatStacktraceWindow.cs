#if UNITY_EDITOR || PLATFORM_ANDROID
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using System.Collections.Generic;
using System.Linq;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatStacktraceWindow : EditorWindow
    {
        static readonly string m_RedColor = "#ff0000ff";
        static readonly string m_GreenColor = "#00ff00ff";
        static readonly string m_DefaultAddressRegex = @"\s*#\d{2}\s*pc\s([a-fA-F0-9]{8}).*(libunity\.so|libmain\.so)";

        [SerializeField]
        List<string> m_RecentSymbolPaths;

        [SerializeField]
        int m_SelectedSymbolPath;

        [SerializeField]
        string m_AddressRegex;

        Vector2 m_ScrollPosition;
        string m_Text = String.Empty;
        string m_ResolvedStacktraces = String.Empty;
        
        
        bool m_ShowResolved;

        GUISkin m_MonoSkin;

        [MenuItem("Stacktrace/Stack")]
        public static void ShowStacktraceWindow()
        {
            var wnd = GetWindow<AndroidLogcatStacktraceWindow>();
            if (wnd == null)
                wnd = ScriptableObject.CreateInstance<AndroidLogcatStacktraceWindow>();
            wnd.Show();
            wnd.Focus();
        }

        private bool ParseLine(Regex regex, string msg, out string address, out string libName)
        {
            var match = regex.Match(msg);
            if (match.Success)
            {
                address = match.Groups[1].Value;
                libName = match.Groups[2].Value;
                return true;
            }
            address = null;
            libName = null;
            return false;
        }

        string GetSymbolFile(string symbolPath, string libraryFile)
        {
            var fullPath = Path.Combine(symbolPath, libraryFile);
            if (File.Exists(fullPath))
                return fullPath;

            // Try sym.so extension
            fullPath = Path.Combine(symbolPath, Path.GetFileNameWithoutExtension(libraryFile) + ".sym.so");
            if (File.Exists(fullPath))
                return fullPath;

            return null;
        }

        void AddSymbolPath(string path)
        {
            int index = m_RecentSymbolPaths.IndexOf(path);
            if (index >= 0)
                m_RecentSymbolPaths.RemoveAt(index);

            m_RecentSymbolPaths.Insert(0, path);
            if (m_RecentSymbolPaths.Count > 10)
                m_RecentSymbolPaths.RemoveAt(m_RecentSymbolPaths.Count - 1);

            m_SelectedSymbolPath = 0;
        }

        static string ConvertSlashToUnicodeSlash(string text_)
        {
            return text_.Replace("/", " \u2215");
        }


        void ResolveStacktraces(string symbolPath)
        {
            m_ResolvedStacktraces = String.Empty;
            var regex = new Regex(m_AddressRegex);
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
                    var symbolFile = GetSymbolFile(symbolPath, library);
                    if (string.IsNullOrEmpty(symbolFile))
                    {
                        resolved = string.Format(" <color={0}>({1} not found)</color>", m_RedColor, library);

                    }
                    else
                    {

                        Debug.Log(address + " " + library);

                        try
                        {
                            // TODO: quates
                            var result = Addr2LineWrapper.Run("\"" + symbolFile + "\"", new[] { address });
                            if (!string.IsNullOrEmpty(result[0]))
                                resolved = string.Format(" <color={0}>({1})</color>", m_GreenColor, result[0].Trim());
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError(ex.Message);
                            return;
                            //for (int i = 0; i < addresses.Count; i++)
                            //{
                            //    var idx = addresses[i].logEntryIndex;
                            //    entries[idx] = new LogEntry(entries[idx]) { message = ModifyLogEntry(entries[idx].message, "(Addr2Line failure)", true) };
                            //    var errorMessage = new StringBuilder();
                            //    errorMessage.AppendLine("Addr2Line failure");
                            //    errorMessage.AppendLine("Scripting Backend: " + buildInfo.scriptingImplementation);
                            //    errorMessage.AppendLine("Build Type: " + buildInfo.buildType);
                            //    errorMessage.AppendLine("CPU: " + buildInfo.cpu);
                            //    errorMessage.AppendLine(ex.Message);
                            //    UnityEngine.Debug.LogError(errorMessage.ToString());
                            //}
                        }
                    }

                    m_ResolvedStacktraces += l.Replace(address, address + resolved);
                }

                m_ResolvedStacktraces += Environment.NewLine;
            }
        }

        private void OnEnable()
        {
            var data = EditorPrefs.GetString(GetType().FullName, JsonUtility.ToJson(this, false));
            JsonUtility.FromJsonOverwrite(data, this);

            if (m_RecentSymbolPaths == null)
                m_RecentSymbolPaths = new List<string>();
            else
            {
                var validatedSymbolPaths = new List<string>();
                foreach (var s in m_RecentSymbolPaths)
                {
                    if (!Directory.Exists(s))
                        continue;
                    validatedSymbolPaths.Add(s);
                }
                m_RecentSymbolPaths = validatedSymbolPaths;
            }

            if (m_SelectedSymbolPath >= m_RecentSymbolPaths.Count)
                m_SelectedSymbolPath = (m_RecentSymbolPaths.Count == 0) ? -1 : 0;

            if (string.IsNullOrEmpty(m_AddressRegex))
                m_AddressRegex = m_DefaultAddressRegex;

            m_MonoSkin = AssetDatabase.LoadAssetAtPath<GUISkin>("Packages/com.unity.mobile.android-logcat/Editor/Resources/Skins/MonoSpaceSkin.guiskin");
        }

        private void OnDisable()
        {
            var data = JsonUtility.ToJson(this, false);
            EditorPrefs.SetString(GetType().FullName, data);
        }

        void DoSymbolPath(float labelWidth)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Symbol path:", GUILayout.Width(labelWidth));

            var recentPaths = new List<string>(m_RecentSymbolPaths);
            recentPaths.Add("");
            recentPaths.Add("Select Symbol Path");

            int selection = EditorGUILayout.Popup(m_SelectedSymbolPath, recentPaths.Select(m => new GUIContent(ConvertSlashToUnicodeSlash(m))).ToArray());
            if (selection == m_RecentSymbolPaths.Count + 1)
            {
                var symbolPath = m_SelectedSymbolPath >= 0 && m_SelectedSymbolPath < m_RecentSymbolPaths.Count ? m_RecentSymbolPaths[m_SelectedSymbolPath] : EditorApplication.applicationContentsPath;
                symbolPath = EditorUtility.OpenFolderPanel("Locate symbol path", symbolPath, "");
                if (!string.IsNullOrEmpty(symbolPath))
                    AddSymbolPath(symbolPath);
            }
            else if (selection >= 0 && selection < m_RecentSymbolPaths.Count)
            {
                m_SelectedSymbolPath = selection;
            }
            EditorGUILayout.EndHorizontal();
        }

        void DoRegex(float labelWidth, float buttonWidth)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Regex:", GUILayout.Width(labelWidth));
            m_AddressRegex = GUILayout.TextField(m_AddressRegex);
            EditorGUI.BeginDisabledGroup(m_SelectedSymbolPath < 0);
            if (GUILayout.Button("Reset Regex", GUILayout.Width(buttonWidth)))
            {
                m_AddressRegex = m_DefaultAddressRegex;
            }
            if (GUILayout.Button("Resolve", GUILayout.Width(buttonWidth)))
            {
                ResolveStacktraces(m_RecentSymbolPaths[m_SelectedSymbolPath]);
                GUIUtility.keyboardControl = 0;
                GUIUtility.hotControl = 0;
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        void OnGUI()
        {
            const float kLabelWidth = 80.0f;
            const float kButtonWidth = 80.0f;

            GUILayout.Box("", AndroidLogcatStyles.columnHeader, GUILayout.Width(position.width), GUILayout.Height(40));
            GUILayout.BeginArea(new Rect(0, 0, this.position.width, 40));
            DoSymbolPath(kLabelWidth);
            DoRegex(kLabelWidth, kButtonWidth);
            GUILayout.EndArea();

            EditorGUI.BeginChangeCheck();
            m_ShowResolved = GUILayout.Toggle(m_ShowResolved, "Resolved");
            if (EditorGUI.EndChangeCheck())
            {
                // Editor seems to be caching text from EditorGUILayout.TextArea
                // This invalidates the cache, and forces the text to change in text area
                GUIUtility.keyboardControl = 0;
                GUIUtility.hotControl = 0;
            }

            m_ScrollPosition = GUILayout.BeginScrollView(m_ScrollPosition);
            if (m_ShowResolved)
                EditorGUILayout.TextArea(m_ResolvedStacktraces, m_MonoSkin.textArea, GUILayout.ExpandHeight(true));
            else
                m_Text = EditorGUILayout.TextArea(m_Text, m_MonoSkin.textArea, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
        }
    }
}
#endif
