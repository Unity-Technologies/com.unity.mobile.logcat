using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

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

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Fill in the placeholder values:", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField(m_OriginalCommand, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(8);

            foreach (var placeholder in m_Placeholders)
            {
                placeholder.value = EditorGUILayout.TextField($"<{placeholder.token}>", placeholder.value);
            }

            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
                Close();

            if (GUILayout.Button("Run", GUILayout.Width(80)))
            {
                var resolved = m_OriginalCommand;
                foreach (var placeholder in m_Placeholders)
                {
                    resolved = resolved.Replace($"<{placeholder.token}>", placeholder.value);
                }

                m_OnConfirm?.Invoke(resolved);
                Close();
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
