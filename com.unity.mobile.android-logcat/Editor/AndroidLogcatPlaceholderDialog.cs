using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatPlaceholderDialog : EditorWindow
    {
        static readonly Regex s_PlaceholderRegex = new Regex(@"<([^>]+)>", RegexOptions.Compiled);

        string m_OriginalCommand;
        Action<string> m_OnConfirm;
        List<PlaceholderInfo> m_Placeholders = new List<PlaceholderInfo>();

        class PlaceholderInfo
        {
            internal string token;
            internal string value;
        }

        internal static void Show(string command, Action<string> onConfirm)
        {
            var matches = s_PlaceholderRegex.Matches(command);
            if (matches.Count == 0)
            {
                onConfirm?.Invoke(command);
                return;
            }

            var wnd = CreateInstance<AndroidLogcatPlaceholderDialog>();
            wnd.titleContent = new GUIContent("Resolve Placeholders");
            wnd.m_OriginalCommand = command;
            wnd.m_OnConfirm = onConfirm;
            wnd.minSize = new Vector2(400, 150);
            wnd.maxSize = new Vector2(600, 400);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in matches)
            {
                var token = match.Groups[1].Value;
                if (!seen.Add(token))
                    continue;

                var defaultValue = "";
                if (token.Equals("package", StringComparison.OrdinalIgnoreCase))
                    defaultValue = PlayerSettings.applicationIdentifier;

                wnd.m_Placeholders.Add(new PlaceholderInfo { token = token, value = defaultValue });
            }

            wnd.ShowUtility();
        }

        void OnEnable()
        {
            LoadUI();
        }

        void LoadUI()
        {
            var r = rootVisualElement;
            var tree = AndroidLogcatUtilities.LoadUXML("AndroidLogcatPlaceholder.uxml");
            tree.CloneTree(r);

            r.Q<Label>("CommandPreview").text = m_OriginalCommand ?? "";

            var container = r.Q<VisualElement>("PlaceholderContainer");
            foreach (var placeholder in m_Placeholders)
            {
                var ph = placeholder;
                var tf = new TextField($"<{ph.token}>");
                tf.value = ph.value;
                tf.RegisterValueChangedCallback(evt => ph.value = evt.newValue);
                container.Add(tf);
            }

            r.Q<Button>("CancelButton").clicked += () => Close();
            r.Q<Button>("RunButton").clicked += OnRunClicked;
        }

        void OnRunClicked()
        {
            var placeholderValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var placeholder in m_Placeholders)
                placeholderValues[placeholder.token] = placeholder.value;

            var resolved = s_PlaceholderRegex.Replace(m_OriginalCommand, match =>
            {
                var token = match.Groups[1].Value;
                return placeholderValues.TryGetValue(token, out var value) ? value : match.Value;
            });

            m_OnConfirm?.Invoke(resolved);
            Close();
        }
    }
}
