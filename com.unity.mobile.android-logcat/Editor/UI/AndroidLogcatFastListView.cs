using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// Fast way of drawing List view via IMGUI
    /// We only render visible items, making it possible to have 10,000 or more items without significant performance penalty
    /// </summary>
    internal class AndroidLogcatFastListView
    {
        internal class Entry
        {
            public bool Selected { get; set; }
            public string Value { get; set; }
        }

        readonly Func<GUIStyle> GetStyle;
        readonly uint m_MaxEntries;

        List<Entry> m_LogEntries = new List<Entry>();
        Vector2 m_ScrollPosition = Vector2.zero;
        Rect m_ScrollArea;
        float m_MaxEntryWidth;
        bool m_RecalculateMaxEntryWidth;

        internal IReadOnlyList<Entry> Entries => m_LogEntries;

        internal AndroidLogcatFastListView(Func<GUIStyle> getStyle, uint maxEntries)
        {
            GetStyle = getStyle;
            m_MaxEntries = maxEntries;
        }

        /// <summary>
        /// Add new entries, can be called from different threads
        /// </summary>
        /// <param name="newEntries"></param>
        internal void AddEntries(string[] newEntries)
        {
            lock (m_LogEntries)
            {
                var start = m_LogEntries.Count;
                m_LogEntries.AddRange(new Entry[newEntries.Length]);
                var entries = new Entry[newEntries.Length];
                for (int i = 0; i < entries.Length; i++)
                {
                    m_LogEntries[start + i] = new Entry()
                    {
                        Value = newEntries[i]
                    };
                }

                if (m_LogEntries.Count > m_MaxEntries)
                    m_LogEntries.RemoveRange(0, m_LogEntries.Count - (int)m_MaxEntries);
                m_RecalculateMaxEntryWidth = true;
            }
        }

        internal void ClearEntries()
        {
            lock (m_LogEntries)
            {
                m_LogEntries.Clear();
                m_RecalculateMaxEntryWidth = true;
            }
        }

        // Since we're not drawing all entries, we need to calculate max width, so scroll view can be sized correctly
        private void RecalculateEntriesMaxWidthIfNeeded()
        {
            if (!m_RecalculateMaxEntryWidth)
                return;
            m_RecalculateMaxEntryWidth = false;
            m_MaxEntryWidth = 0;
            for (int i = 0; i < m_LogEntries.Count; i++)
            {
                m_MaxEntryWidth = Mathf.Max(m_MaxEntryWidth, GetStyle().CalcSize(new GUIContent(m_LogEntries[i].Value)).x);
            }
        }

        internal bool OnGUI()
        {
            RecalculateEntriesMaxWidthIfNeeded();

            DoEntriesGUI();

            var logHeight = GetStyle().fixedHeight;
            var e = Event.current;

            m_ScrollPosition = GUILayout.BeginScrollView(m_ScrollPosition, true, true);
            GUILayoutUtility.GetRect(m_MaxEntryWidth, m_LogEntries.Count * logHeight);
            GUILayout.EndScrollView();

            if (e.type == EventType.Repaint)
                m_ScrollArea = GUILayoutUtility.GetLastRect();

            if (HandleMouseAndKeyboardControls())
                return true;

            return false;
        }

        private void DoEntriesGUI()
        {
            if (Event.current.type != EventType.Repaint)
                return;

            var logHeight = GetStyle().fixedHeight;
            var startIdx = (int)(m_ScrollPosition.y / logHeight);
            var displayCount = m_ScrollArea.height / logHeight;

            for (int i = 0; i < m_LogEntries.Count; i++)
            {
                if (i < startIdx || i >= startIdx + displayCount)
                    continue;
                var entry = m_LogEntries[i];
                var rc = new Rect(-m_ScrollPosition.x, m_ScrollArea.y + (i - startIdx) * logHeight, m_MaxEntryWidth, logHeight);
                GetStyle().Draw(rc, entry.Value, false, false, entry.Selected, false);
            }
        }

        private bool HandleMouseAndKeyboardControls()
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown)
                return false;

            if (e.button == 0)
            {
                var idx = (int)((e.mousePosition.y - m_ScrollArea.y + (int)(m_ScrollPosition.y / GetStyle().fixedHeight) * GetStyle().fixedHeight) / (float)GetStyle().fixedHeight);
                if (idx < m_LogEntries.Count)
                {
                    if (!e.HasCtrlOrCmdModifier())
                    {
                        foreach (var entry in m_LogEntries)
                            entry.Selected = false;
                    }

                    m_LogEntries[idx].Selected = !m_LogEntries[idx].Selected;
                    return true;
                }
            }
            else if (e.button == 1)
            {
                var menuItems = new[] { new GUIContent("Copy"), new GUIContent("Select All") };
                EditorUtility.DisplayCustomMenu(new Rect(e.mousePosition.x, e.mousePosition.y, 0, 0),
                    menuItems.ToArray(), -1, MenuSelection, null);
            }

            return false;
        }

        private void MenuSelection(object userData, string[] options, int selected)
        {
            switch (selected)
            {
                // Copy
                case 0:
                    var builder = new StringBuilder();
                    foreach (var entry in m_LogEntries)
                    {
                        if (!entry.Selected)
                            continue;
                        builder.AppendLine(entry.Value);
                    }

                    EditorGUIUtility.systemCopyBuffer = builder.ToString();
                    break;
                // Select All
                case 1:
                    foreach (var entry in m_LogEntries)
                        entry.Selected = true;
                    break;
            }
        }
    }
}
