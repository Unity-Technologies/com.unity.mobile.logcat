using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatCommandOutputWindow : EditorWindow
    {
        static AndroidLogcatCommandOutputWindow s_Instance;

        readonly List<AndroidLogcatCommandsWindow.OutputLine> m_Lines = new List<AndroidLogcatCommandsWindow.OutputLine>();
        bool m_AutoScroll = true;
        const int kMaxLines = 5000;

        ScrollView m_ScrollView;
        VisualElement m_OutputContainer;

        internal static void Open(List<AndroidLogcatCommandsWindow.OutputLine> existingLines)
        {
            var wnd = GetWindow<AndroidLogcatCommandOutputWindow>();
            wnd.titleContent = new GUIContent("Command Output");
            wnd.minSize = new Vector2(500, 300);
            s_Instance = wnd;

            wnd.m_Lines.Clear();
            if (existingLines != null)
                wnd.m_Lines.AddRange(existingLines);
            wnd.RefreshOutput();
        }

        internal static void AppendIfOpen(string text, Color color)
        {
            if (s_Instance == null || string.IsNullOrEmpty(text))
                return;

            foreach (var line in text.Split('\n'))
            {
                var ol = new AndroidLogcatCommandsWindow.OutputLine { text = line, color = color };
                s_Instance.m_Lines.Add(ol);
                s_Instance.AddLineElement(ol);
            }

            if (s_Instance.m_Lines.Count > kMaxLines)
            {
                var excess = s_Instance.m_Lines.Count - kMaxLines;
                s_Instance.m_Lines.RemoveRange(0, excess);
                for (int i = 0; i < excess && s_Instance.m_OutputContainer.childCount > 0; i++)
                    s_Instance.m_OutputContainer.RemoveAt(0);
            }

            if (s_Instance.m_AutoScroll)
                s_Instance.ScrollToBottom();
        }

        void OnEnable()
        {
            s_Instance = this;
            LoadUI();
        }

        void LoadUI()
        {
            var r = rootVisualElement;
            var tree = AndroidLogcatUtilities.LoadUXML("AndroidLogcatCommandOutput.uxml");
            tree.CloneTree(r);

            m_ScrollView = r.Q<ScrollView>("OutputScroll");
            m_OutputContainer = r.Q<VisualElement>("OutputContainer");

            r.Q<Button>("ClearButton").clicked += () => { m_Lines.Clear(); m_OutputContainer?.Clear(); };
            r.Q<Button>("CopyAllButton").clicked += CopyAll;

            var autoScrollToggle = r.Q<Toggle>("AutoScrollToggle");
            autoScrollToggle.value = m_AutoScroll;
            autoScrollToggle.RegisterValueChangedCallback(evt => m_AutoScroll = evt.newValue);

            foreach (var line in m_Lines)
                AddLineElement(line);
        }

        void AddLineElement(AndroidLogcatCommandsWindow.OutputLine line)
        {
            var label = new Label(line.text);
            label.style.color = new StyleColor(line.color);
            label.style.whiteSpace = WhiteSpace.Normal;
            m_OutputContainer.Add(label);
        }

        void RefreshOutput()
        {
            if (m_OutputContainer == null)
                return;
            m_OutputContainer.Clear();
            foreach (var line in m_Lines)
                AddLineElement(line);
            if (m_AutoScroll)
                ScrollToBottom();
        }

        void ScrollToBottom()
        {
            if (m_ScrollView == null || m_OutputContainer == null || m_OutputContainer.childCount == 0)
                return;
            m_ScrollView.schedule.Execute(() => m_ScrollView.ScrollTo(m_OutputContainer[m_OutputContainer.childCount - 1])).StartingIn(10);
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
