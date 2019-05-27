#if UNITY_EDITOR || PLATFORM_ANDROID
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Text.RegularExpressions;
using UnityEditor.Android;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatStacktraceWindow : EditorWindow
    {
        Vector2 m_ScrollPosition;
        string m_Text = String.Empty;
        string m_ResolvedStacktraces = String.Empty;
        string m_SymbolPath = String.Empty;
        string m_AddressRegex = @"\s*#\d{2}\s*pc\s([a-fA-F0-9]{8}).*(libunity\.so|libmain\.so)";
        bool m_ShowResolved;

        [MenuItem("Stacktrace/Stack")]
        public static new void Show()
        {
            AndroidLogcatStacktraceWindow win = EditorWindow.GetWindow<AndroidLogcatStacktraceWindow>("Stacktrace Window");
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

        void ResolveStacktraces()
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
                    var symbolFile = GetSymbolFile(m_SymbolPath, library);
                    if (string.IsNullOrEmpty(symbolFile))
                    {
                        m_ResolvedStacktraces += l;
                        continue;
                    }

                    Debug.Log(address + " " + library);
                    string resolved = "(Not resolved)";
                    try
                    {
                        // TODO: quates
                        var result = Addr2LineWrapper.Run("\"" + symbolFile + "\"", new[] { address });
                        if (!string.IsNullOrEmpty(result[0]))
                            resolved = " (" + result[0].Trim() + ")";
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

                    m_ResolvedStacktraces += l.Replace(address, address + resolved);
                }

                m_ResolvedStacktraces += Environment.NewLine;
            }
        }

        void OnGUI()
        {
            const float kLabelWidth = 80.0f;
            const float kButtonWidth = 80.0f;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Symbol path:", GUILayout.Width(kLabelWidth));
            var labelRect = GUILayoutUtility.GetLastRect();
            m_SymbolPath = GUILayout.TextField(m_SymbolPath);
            if (GUILayout.Button("Locate", GUILayout.Width(kButtonWidth)))
            {
                m_SymbolPath = EditorUtility.OpenFolderPanel("Locate symbol path", EditorApplication.applicationContentsPath, "");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Regex:", GUILayout.Width(kLabelWidth));
            m_AddressRegex = GUILayout.TextField(m_AddressRegex);
            if (GUILayout.Button("Resolve", GUILayout.Width(kButtonWidth)))
            {
                ResolveStacktraces();
                GUIUtility.keyboardControl = 0;
                GUIUtility.hotControl = 0;
            }
            EditorGUILayout.EndHorizontal();

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
                EditorGUILayout.TextArea(m_ResolvedStacktraces, GUILayout.ExpandHeight(true));
            else
                m_Text = EditorGUILayout.TextArea(m_Text, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
            /*

            try
            {
                var result = Addr2LineWrapper.Run(libpath, addresses.Select(m => m.unresolvedAddress));
                for (int i = 0; i < addresses.Count; i++)
                {
                    var idx = addresses[i].logEntryIndex;
                    var append = string.IsNullOrEmpty(result[i]) ? "(Not Resolved)" : result[i];
                    entries[idx] = new LogEntry(entries[idx]) { message = ModifyLogEntry(entries[idx].message, append, false) };
                }
            }
            catch (Exception ex)
            {
                for (int i = 0; i < addresses.Count; i++)
                {
                    var idx = addresses[i].logEntryIndex;
                    entries[idx] = new LogEntry(entries[idx]) { message = ModifyLogEntry(entries[idx].message, "(Addr2Line failure)", true) };
                    var errorMessage = new StringBuilder();
                    errorMessage.AppendLine("Addr2Line failure");
                    errorMessage.AppendLine("Scripting Backend: " + buildInfo.scriptingImplementation);
                    errorMessage.AppendLine("Build Type: " + buildInfo.buildType);
                    errorMessage.AppendLine("CPU: " + buildInfo.cpu);
                    errorMessage.AppendLine(ex.Message);
                    UnityEngine.Debug.LogError(errorMessage.ToString());
                }
            }
            */
        }
    }
}
#endif
