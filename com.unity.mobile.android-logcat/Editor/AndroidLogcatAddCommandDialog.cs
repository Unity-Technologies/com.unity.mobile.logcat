using System;
using UnityEditor;
using UnityEngine;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatAddCommandDialog : EditorWindow
    {
        string m_Name = "";
        string m_Command = "";
        Action<AndroidLogcatCommandEntry> m_OnSave;
        bool m_IsEdit;
        Vector2 m_ScrollPos;

        internal static void Show(Action<AndroidLogcatCommandEntry> onSave, AndroidLogcatCommandEntry existing = null)
        {
            var wnd = CreateInstance<AndroidLogcatAddCommandDialog>();
            wnd.titleContent = new GUIContent(existing != null ? "Edit Command" : "Add Command");
            wnd.m_OnSave = onSave;
            wnd.minSize = new Vector2(400, 200);
            wnd.maxSize = new Vector2(600, 400);

            if (existing != null)
            {
                wnd.m_Name = existing.name;
                wnd.m_Command = existing.command;
                wnd.m_IsEdit = true;
            }

            wnd.ShowUtility();
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8);

            m_Name = EditorGUILayout.TextField("Name", m_Name);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Commands", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Enter one command per line. All commands run sequentially.",
                EditorStyles.miniLabel);

            m_ScrollPos = EditorGUILayout.BeginScrollView(m_ScrollPos, GUILayout.MinHeight(80), GUILayout.MaxHeight(200));
            m_Command = EditorGUILayout.TextArea(m_Command, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
                Close();

            if (GUILayout.Button(m_IsEdit ? "Save" : "Add", GUILayout.Width(80)))
            {
                if (string.IsNullOrWhiteSpace(m_Name) || string.IsNullOrWhiteSpace(m_Command))
                {
                    EditorUtility.DisplayDialog("Android Logcat", "Both name and command are required.", "OK");
                    return;
                }

                m_OnSave?.Invoke(new AndroidLogcatCommandEntry(m_Name.Trim(), m_Command.Trim()));
                Close();
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
