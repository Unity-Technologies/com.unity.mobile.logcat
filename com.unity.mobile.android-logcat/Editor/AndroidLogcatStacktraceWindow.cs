using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System.Text;
using System.IO;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatStacktraceWindow : EditorWindow
    {
        static readonly string m_RedColor = "#ff0000ff";
        static readonly string m_GreenColor = "#00ff00ff";
        static readonly string m_YellowColor = "#ffff00ff";


        internal class AndroidStackFrame
        {
            internal string Address { set; get; } = string.Empty;
            internal string MethodName { set; get; } = string.Empty;
            internal int LineNumber { set; get; } = -1;
            internal string BuildId { set; get; } = string.Empty;
        }


        class UnresolvedAddresses
        {
            internal class AddressToStackFrame : Dictionary<string, AndroidStackFrame>
            {

            }

            internal struct SymbolFile
            {
                internal string ABI { set; get; }
                internal string Library { set; get; }
            }

            Dictionary<SymbolFile, AddressToStackFrame> m_Addresses = new Dictionary<SymbolFile, AddressToStackFrame>();

            internal IReadOnlyDictionary<SymbolFile, AddressToStackFrame> AdressesPerSymbolFile => m_Addresses;

            private AddressToStackFrame GetOrCreateAddressMap(SymbolFile key)
            {
                if (m_Addresses.TryGetValue(key, out var addresses))
                    return addresses;
                addresses = new AddressToStackFrame();
                m_Addresses[key] = addresses;
                return addresses;
            }

            internal AndroidStackFrame GetStackFrame(SymbolFile key, string address)
            {
                var addresses = GetOrCreateAddressMap(key);
                if (addresses.TryGetValue(address, out var value))
                    return value;

                value = new AndroidStackFrame()
                {
                    Address = address
                };
                addresses[address] = value;
                return value;
            }

            internal IReadOnlyList<SymbolFile> GetKeys()
            {
                return m_Addresses.Keys.ToArray();
            }
        }

        internal class ResolveResult
        {
            internal string Result { get; private set; }
            internal string Errors { get; private set; }

            internal ResolveResult(string result)
            {
                Result = result;
                Errors = string.Empty;
            }

            internal ResolveResult(string result, string errors)
            {
                Result = result;
                Errors = errors;
            }
        }

        enum WindowMode
        {
            OriginalLog,
            ResolvedLog
        }

        Vector2 m_ScrollPosition;
        Vector2 m_ErrorsScrollPosition;
        string m_Text = String.Empty;
        ResolveResult m_ResolveResult;

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

        internal static ResolveResult ResolveAddresses(string[] lines,
            IReadOnlyList<ReordableListItem> regexes,
            IReadOnlyList<ReordableListItem> symbolPaths,
            IReadOnlyList<ReordableListItem> symbolExtensions,
            AndroidTools tools)
        {
            var output = string.Empty;
            var errorsMismatchingBuildIds = new HashSet<string>();
            // Calling addr2line for every address is costly, that's why we need to do it in batch
            var unresolved = new UnresolvedAddresses();
            var frames = new AndroidStackFrame[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                var l = lines[i];
                if (!AndroidLogcatUtilities.ParseCrashLine(regexes, l, out var abi, out var address, out var library, out var buildId))
                {
                    frames[i] = null;
                    continue;
                }

                var frame = unresolved.GetStackFrame(new UnresolvedAddresses.SymbolFile() { ABI = abi, Library = library }, address);
                frame.BuildId = buildId;
                frames[i] = frame;
            }

            var buildIds = new Dictionary<string, string>();

            var keys = unresolved.GetKeys();
            foreach (var key in keys)
            {
                var adressesPerSymbolFile = unresolved.AdressesPerSymbolFile[key];
                var addressKeys = adressesPerSymbolFile.Keys.ToArray();
                var addressValues = adressesPerSymbolFile.Values.ToArray();

                var exts = symbolExtensions.GetEnabledValues();
                var symbolFile = AndroidLogcatUtilities.GetSymbolFile(symbolPaths,
                    key.ABI,
                    key.Library,
                    exts);

                // Symbol file not found, set 'not found' messages for all addresses of this library
                if (string.IsNullOrEmpty(symbolFile))
                {
                    var value = $"<color={m_RedColor}>({Path.GetFileNameWithoutExtension(key.Library)}[{string.Join("|", exts)}] not found)</color>";

                    foreach (var v in addressValues)
                        v.MethodName = value;
                    continue;
                }

                try
                {
                    if (!buildIds.TryGetValue(symbolFile, out var buildId))
                    {
                        buildId = AndroidLogcatUtilities.GetBuildId(tools, symbolFile);
                        buildIds[symbolFile] = buildId;
                    }

                    var result = tools.RunAddr2Line(symbolFile, addressKeys);

                    if (result.Length != addressKeys.Length)
                    {
                        return new ResolveResult($"Failed to run addr2line, expected to receive {addressKeys.Length} addresses, but received {result.Length}");
                    }

                    for (int i = 0; i < addressKeys.Length; i++)
                    {
                        AndroidLogcatInternalLog.Log($"{addressKeys[i]} ---> {result[i]}");

                        var color = m_GreenColor;

                        if (!string.IsNullOrEmpty(buildId) &&
                            !string.IsNullOrEmpty(addressValues[i].BuildId) &&
                            !buildId.Equals(addressValues[i].BuildId))
                        {
                            errorsMismatchingBuildIds.Add($" '{symbolFile}' contains unexpected buildId '{buildId}', expected buildId '{addressValues[i].BuildId}'.");
                            color = m_YellowColor;
                        }

                        addressValues[i].MethodName = $"<color={color}>({result[i].Trim()})</color>";
                    }
                }
                catch (Exception ex)
                {
                    return new ResolveResult($"Exception while running addr2line:\n{ex.Message}");
                }
            }


            for (int i = 0; i < lines.Length; i++)
            {
                var l = lines[i];
                var f = frames[i];
                output += f == null ?
                    l :
                    l.Replace(f.Address, f.Address + " " + f.MethodName);
                output += Environment.NewLine;
            }

            var errors = errorsMismatchingBuildIds.Count == 0 ?
                string.Empty :
                $"<color={m_RedColor}>Wrong symbol files?:\n" + string.Join("\n", errorsMismatchingBuildIds) + "</color>";
            return new ResolveResult(output, errors);
        }

        void ResolveStacktraces()
        {
            m_ResolveResult = null;
            if (string.IsNullOrEmpty(m_Text))
            {
                m_ResolveResult = new ResolveResult($"<color={m_RedColor}>Please add some log with addresses first.</color>");
                return;
            }

            if (m_Runtime.Settings.StacktraceResolveRegex.Count == 0)
            {
                m_ResolveResult = new ResolveResult($"<color={m_RedColor}>No stacktrace regular expressions found.\nClick <b>Configure Regex</b> and configure Stacktrace Regex.</color>");
                return;
            }

            if (m_Runtime.UserSettings.SymbolPaths.Count == 0)
            {
                m_ResolveResult = new ResolveResult($"<color={m_RedColor}>At least one symbol path needs to be specified.\nClick Configure Symbol Paths and add the necessary symbol path.</color>");
                return;
            }


            var lines = m_Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            m_ResolveResult = ResolveAddresses(lines,
                m_Runtime.Settings.StacktraceResolveRegex,
                m_Runtime.UserSettings.SymbolPaths,
                m_Runtime.Settings.SymbolExtensions,
                m_Runtime.Tools);
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
                placeholder.AppendLine("2025/05/26 13:13:11.488 16541 16557 Error CRASH       #01 pc 00000000001fc0a4  /data/app/~~ZPEDQqIxu8AhClGhRR65CA==/com.DefaultCompany.MyGame-gYtNtB9HCft5sX98ZKtsTQ==/lib/arm64/libunity.so (BuildId: 4ae70803f1f19d46fb4148bb3e739e51ae1b0038)");
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
            var oldAlign = GUI.skin.button.alignment;
            GUI.skin.button.alignment = TextAnchor.MiddleLeft;
            if (GUILayout.Button("Configure Regex"))
                SettingsService.OpenUserPreferences(AndroidLogcatSettingsProvider.kSettingsPath);
            if (GUILayout.Button("Configure Symbol Paths"))
                SettingsService.OpenProjectSettings(AndroidLogcatProjectSettingsProvider.kSettingsPath);
            if (GUILayout.Button("Configure Symbol Extensions"))
                SettingsService.OpenUserPreferences(AndroidLogcatSettingsProvider.kSettingsPath);
            GUI.skin.button.alignment = oldAlign;
            EditorGUILayout.EndVertical();
        }


        void ShowErrorsIfNeeded()
        {
            if (m_WindowMode != WindowMode.ResolvedLog || m_ResolveResult == null || string.IsNullOrEmpty(m_ResolveResult.Errors))
                return;

            EditorGUILayout.LabelField("Errors", EditorStyles.boldLabel);
            m_ErrorsScrollPosition = EditorGUILayout.BeginScrollView(m_ErrorsScrollPosition, GUILayout.Height(150));
            EditorGUILayout.TextArea(m_ResolveResult.Errors, AndroidLogcatStyles.resolvedStacktraceStyle, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
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

            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition, GUILayout.ExpandHeight(true));
            GUI.SetNextControlName(WindowMode.ResolvedLog.ToString());
            switch (m_WindowMode)
            {
                case WindowMode.ResolvedLog:
                    var text = m_ResolveResult != null ? m_ResolveResult.Result : string.Empty;
                    // Note: Not using EditorGUILayout.SelectableLabel, because scrollbars are not working correctly
                    EditorGUILayout.TextArea(text, AndroidLogcatStyles.resolvedStacktraceStyle, GUILayout.ExpandHeight(true));
                    break;
                case WindowMode.OriginalLog:
                    m_Text = EditorGUILayout.TextArea(m_Text, AndroidLogcatStyles.stacktraceStyle, GUILayout.ExpandHeight(true));
                    break;
            }

            EditorGUILayout.EndScrollView();
            ShowErrorsIfNeeded();
            GUILayout.EndVertical();
            DoInfoGUI();
            GUILayout.EndHorizontal();
        }
    }
}
