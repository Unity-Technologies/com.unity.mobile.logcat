using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatCommandOutputWindow : EditorWindow
    {
        static AndroidLogcatCommandOutputWindow s_Instance;

        Vector2 m_ScrollPos;
        readonly List<OutputLine> m_Lines = new List<OutputLine>();
        bool m_AutoScroll = true;
        const int kMaxLines = 5000;

        internal struct OutputLine
        {
            internal string text;
            internal Color color;
        }

        internal static void Open(List<AndroidLogcatCommandsWindow.OutputLine> existingLines)
        {
            var wnd = GetWindow<AndroidLogcatCommandOutputWindow>();
            wnd.titleContent = new GUIContent("Command Output");
            wnd.minSize = new Vector2(500, 300);
            s_Instance = wnd;

            wnd.m_Lines.Clear();
            if (existingLines != null)
            {
                foreach (var ol in existingLines)
                    wnd.m_Lines.Add(new OutputLine { text = ol.text, color = ol.color });
            }
        }

        internal static void AppendIfOpen(string text, Color color)
        {
            if (s_Instance == null)
                return;
            if (string.IsNullOrEmpty(text))
                return;

            foreach (var line in text.Split('\n'))
            {
                s_Instance.m_Lines.Add(new OutputLine { text = line, color = color });
            }

            while (s_Instance.m_Lines.Count > kMaxLines)
                s_Instance.m_Lines.RemoveAt(0);

            s_Instance.Repaint();
        }

        void OnEnable()
        {
            s_Instance = this;
        }

        void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton))
                m_Lines.Clear();
            if (GUILayout.Button("Copy All", EditorStyles.toolbarButton))
                CopyAll();
            GUILayout.FlexibleSpace();
            m_AutoScroll = GUILayout.Toggle(m_AutoScroll, "Auto Scroll", EditorStyles.toolbarButton);
            EditorGUILayout.EndHorizontal();

            m_ScrollPos = EditorGUILayout.BeginScrollView(m_ScrollPos);

            for (int i = 0; i < m_Lines.Count; i++)
            {
                var line = m_Lines[i];
                var style = new GUIStyle(EditorStyles.label)
                {
                    wordWrap = true,
                    richText = false
                };
                style.normal.textColor = line.color;
                EditorGUILayout.LabelField(line.text, style);
            }

            EditorGUILayout.EndScrollView();

            if (m_AutoScroll && Event.current.type == EventType.Repaint)
                m_ScrollPos.y = float.MaxValue;
        }

        void CopyAll()
        {
            var sb = new StringBuilder();
            foreach (var line in m_Lines)
                sb.AppendLine(line.text);
            EditorGUIUtility.systemCopyBuffer = sb.ToString();
        }

        void OnDestroy()
        {
            if (s_Instance == this)
                s_Instance = null;
        }
    }
}
