using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System.Text;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatStacktraceWindow : EditorWindow
    {
        static readonly string m_RedColor = "#ff0000ff";
        static readonly string m_GreenColor = "#00ff00ff";

        class UnresolvedAddresses
        {
            internal struct AddressKey
            {
                internal string ABI { set; get; }
                internal string Library { set; get; }
            }

            Dictionary<AddressKey, Dictionary<string, string>> m_Addresses = new Dictionary<AddressKey, Dictionary<string, string>>();

            private Dictionary<string, string> GetOrCreateAddressMap(AddressKey key)
            {
                Dictionary<string, string> addresses;
                if (m_Addresses.TryGetValue(key, out addresses))
                    return addresses;
                addresses = new Dictionary<string, string>();
                m_Addresses[key] = addresses;
                return addresses;
            }

            internal void CreateAddressEntry(AddressKey key, string address)
            {
                var addresses = GetOrCreateAddressMap(key);
                addresses[address] = string.Empty;
            }

            internal void SetAddressValue(AddressKey key, string address, string value)
            {
                var addresses = GetOrCreateAddressMap(key);
                addresses[address] = value;
            }

            internal string GetAddressValue(AddressKey key, string address)
            {
                var addresses = GetOrCreateAddressMap(key);
                string value = string.Empty;
                if (addresses.TryGetValue(address, out value))
                    return value;
                return string.Empty;
            }

            internal IReadOnlyList<AddressKey> GetKeys()
            {
                return m_Addresses.Keys.ToArray();
            }

            internal IReadOnlyList<string> GetAllAddresses(AddressKey key)
            {
                return m_Addresses[key].Keys.ToArray();
            }
        }

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

        internal static string ResolveAddresses(string[] lines, IReadOnlyList<ReordableListItem> regexes, IReadOnlyList<ReordableListItem> symbolPaths, AndroidTools tools)
        {
            var output = string.Empty;
            // Calling addr2line for every address is costly, that's why we need to do it in batch
            var unresolved = new UnresolvedAddresses();
            foreach (var l in lines)
            {
                string address;
                string library;
                string abi;
                if (!AndroidLogcatUtilities.ParseCrashLine(regexes, l, out abi, out address, out library))
                    continue;
                unresolved.CreateAddressEntry(new UnresolvedAddresses.AddressKey() { ABI = abi, Library = library }, address);
            }

            var keys = unresolved.GetKeys();
            foreach (var key in keys)
            {
                var addresses = unresolved.GetAllAddresses(key);
                var symbolFile = AndroidLogcatUtilities.GetSymbolFile(symbolPaths, key.ABI, key.Library);

                // Symbol file not found, set 'not found' messages for all addresses of this library
                if (string.IsNullOrEmpty(symbolFile))
                {
                    var value = $"<color={m_RedColor}>({key.Library} not found)</color>";
                    foreach (var a in addresses)
                        unresolved.SetAddressValue(key, a, value);
                    continue;
                }

                try
                {
                    var result = tools.RunAddr2Line(symbolFile, addresses.ToArray());

                    if (result.Length != addresses.Count)
                    {
                        return $"Failed to run addr2line, expected to receive {addresses.Count} addresses, but received {result.Length}";
                    }

                    for (int i = 0; i < addresses.Count; i++)
                    {
                        AndroidLogcatInternalLog.Log($"{addresses[i]} ---> {result[i]}");
                        unresolved.SetAddressValue(key, addresses[i], $"<color={m_GreenColor}>({result[i].Trim()})</color>");
                    }
                }
                catch (Exception ex)
                {
                    return $"Exception while running addr2line:\n{ex.Message}";
                }
            }


            foreach (var l in lines)
            {
                string address;
                string library;
                string abi;
                if (!AndroidLogcatUtilities.ParseCrashLine(regexes, l, out abi, out address, out library))
                {
                    output += l;
                }
                else
                {
                    output += l.Replace(address, address + " " + unresolved.GetAddressValue(new UnresolvedAddresses.AddressKey() { ABI = abi, Library = library }, address));
                }

                output += Environment.NewLine;
            }

            return output;
        }

        void ResolveStacktraces()
        {
            m_ResolvedStacktraces = String.Empty;
            if (string.IsNullOrEmpty(m_Text))
            {
                m_ResolvedStacktraces = $"<color={m_RedColor}>Please add some log with addresses first.</color>";
                return;
            }

            if (m_Runtime.Settings.StacktraceResolveRegex.Count == 0)
            {
                m_ResolvedStacktraces = $"<color={m_RedColor}>No stacktrace regular expressions found.\nClick <b>Configure Regex</b> and configure Stacktrace Regex.</color>";
                return;
            }

            if (m_Runtime.UserSettings.SymbolPaths.Count == 0)
            {
                m_ResolvedStacktraces = $"<color={m_RedColor}>At least one symbol path needs to be specified.\nClick Configure Symbol Paths and add the necessary symbol path.</color>";
                return;
            }


            var lines = m_Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            m_ResolvedStacktraces = ResolveAddresses(lines, m_Runtime.Settings.StacktraceResolveRegex, m_Runtime.UserSettings.SymbolPaths, m_Runtime.Tools);
        }

        private void OnEnable()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
                return;

            m_Runtime = AndroidLogcatManager.instance.Runtime;
            if (string.IsNullOrEmpty(m_Text))
            {
                var placeholder = new StringBuilder();
                placeholder.AppendLine("Copy paste log with address and click Resolve Stackraces");
                placeholder.AppendLine("For example:");
                placeholder.AppendLine("2019-05-17 12:00:58.830 30759-30803/? E/CRASH: \t#00  pc 002983fc  /data/app/com.mygame==/lib/arm/libunity.so");
                m_Text = placeholder.ToString();
            }
        }

        private void SelectWindowMode(WindowMode mode)
        {
            m_WindowMode = mode;

            GUIUtility.keyboardControl = 0;
            GUIUtility.hotControl = 0;
            GUI.FocusControl(string.Empty);
            Repaint();
        }

        void DoInfoGUI()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(100));

            if (GUILayout.Button("Resolve Stacktraces"))
            {
                // Note: Must be executed before ResolveStacktraces, otherwise m_Text might contain old data
                SelectWindowMode(WindowMode.ResolvedLog);

                ResolveStacktraces();
            }
            GUILayout.Space(20);
            if (GUILayout.Button("Configure Regex"))
                SettingsService.OpenUserPreferences(AndroidLogcatSettingsProvider.kSettingsPath);
            if (GUILayout.Button("Configure Symbol Paths"))
                SettingsService.OpenProjectSettings(AndroidLogcatProjectSettingsProvider.kSettingsPath);
            EditorGUILayout.EndVertical();
        }

        void OnGUI()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
            {
                AndroidLogcatUtilities.ShowAndroidIsNotInstalledMessage();
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            EditorGUI.BeginChangeCheck();
            m_WindowMode = (WindowMode)GUILayout.Toolbar((int)m_WindowMode, new[] { new GUIContent("Original"), new GUIContent("Resolved"), }, "LargeButton", GUI.ToolbarButtonSize.Fixed, GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck())
                SelectWindowMode(m_WindowMode);

            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);
            GUI.SetNextControlName(WindowMode.ResolvedLog.ToString());
            switch (m_WindowMode)
            {
                case WindowMode.ResolvedLog:
                    // Note: Not using EditorGUILayout.SelectableLabel, because scrollbars are not working correctly
                    EditorGUILayout.TextArea(m_ResolvedStacktraces, AndroidLogcatStyles.resolvedStacktraceStyle, GUILayout.ExpandHeight(true));
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
    }
}
