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
                if (m_LogEntries.Count + newEntries.Length > m_MaxEntries)
                    m_LogEntries.RemoveRange(0, Math.Min(m_LogEntries.Count, m_LogEntries.Count + newEntries.Length - (int)m_MaxEntries));

                var start = m_LogEntries.Count;
                var startForIncoming = 0;
                var lengthForIncoming = newEntries.Length;

                if (lengthForIncoming > m_MaxEntries)
                {
                    startForIncoming = lengthForIncoming - (int)m_MaxEntries;
                    lengthForIncoming = (int)m_MaxEntries;
                }

                m_LogEntries.AddRange(new Entry[lengthForIncoming]);
                var entries = new Entry[lengthForIncoming];
                for (int i = 0; i < lengthForIncoming; i++)
                {
                    m_LogEntries[start + i] = new Entry()
                    {
                        Value = newEntries[startForIncoming + i]
                    };
                }

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
            var longestText = string.Empty;
            m_MaxEntryWidth = 0.0f;
            for (int i = 0; i < m_LogEntries.Count; i++)
            {
                if (m_LogEntries[i].Value.Length > longestText.Length)
                    longestText = m_LogEntries[i].Value;
            }

            if (longestText.Length > 0)
                m_MaxEntryWidth = Mathf.Max(m_MaxEntryWidth, GetStyle().CalcSize(new GUIContent(longestText)).x);
        }

        internal bool OnGUI()
        {
            RecalculateEntriesMaxWidthIfNeeded();
            var logHeight = GetStyle().fixedHeight;
            var entriesView = new Rect(0, 0, m_MaxEntryWidth, logHeight * m_LogEntries.Count);

            var rc = GUILayoutUtility.GetRect(GUIContent.none, GetStyle(), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            var controlId = GUIUtility.GetControlID(FocusType.Keyboard);
            m_ScrollPosition = GUI.BeginScrollView(rc, m_ScrollPosition, entriesView, true, true);
            DoEntries((int)(m_LogEntries.Count * m_ScrollPosition.y / entriesView.height), (int)(rc.height / logHeight));
            GUI.EndScrollView();

            // Note: Mouse handling must come before keys, since it sets keyboard control
            if (HandleMouse(rc, controlId))
                return true;
            if (HandleKeys(controlId))
                return true;


            return false;
        }

        private void DoEntries(int startIdx, int displayCount)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            var logHeight = GetStyle().fixedHeight;
            for (int i = 0; i < m_LogEntries.Count; i++)
            {
                if (i < startIdx || i >= startIdx + displayCount)
                    continue;
                var entry = m_LogEntries[i];
                var rc = new Rect(0, m_ScrollPosition.y + (i - startIdx) * logHeight, m_MaxEntryWidth, logHeight);
                GetStyle().Draw(rc, entry.Value, false, false, entry.Selected, false);
            }
        }

        private void DoCopy()
        {
            var builder = new StringBuilder();
            foreach (var entry in m_LogEntries)
            {
                if (!entry.Selected)
                    continue;
                builder.AppendLine(entry.Value);
            }

            EditorGUIUtility.systemCopyBuffer = builder.ToString();
        }

        private void DoSelectAll()
        {
            foreach (var entry in m_LogEntries)
                entry.Selected = true;
        }

        private bool HandleMouse(Rect view, int controldId)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown)
            {
                var idx = (int)((e.mousePosition.y - view.y + (int)(m_ScrollPosition.y / GetStyle().fixedHeight) * GetStyle().fixedHeight) / (float)GetStyle().fixedHeight);
                if (idx < m_LogEntries.Count)
                {
                    if (!m_LogEntries[idx].Selected)
                    {
                        if (!e.HasCtrlOrCmdModifier())
                        {
                            foreach (var entry in m_LogEntries)
                                entry.Selected = false;
                        }
                        m_LogEntries[idx].Selected = true;
                    }

                    return true;
                }

                GUIUtility.keyboardControl = controldId;
            }

            if (e.type == EventType.MouseUp && e.button == 1)
            {
                var menuItems = new[] { new GUIContent("Copy"), new GUIContent("Select All") };
                EditorUtility.DisplayCustomMenu(new Rect(e.mousePosition.x, e.mousePosition.y, 0, 0),
                    menuItems.ToArray(), -1, MenuSelection, null);
            }

            return false;
        }

        private bool HandleKeys(int controlId)
        {
            if (GUIUtility.keyboardControl != controlId)
                return false;

            var requestRepaint = false;
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                bool hasCtrlOrCmd = e.HasCtrlOrCmdModifier();
                switch (e.keyCode)
                {
                    case KeyCode.A:
                        if (hasCtrlOrCmd)
                        {
                            DoSelectAll();
                            e.Use();
                            requestRepaint = true;
                        }
                        break;
                    case KeyCode.C:
                        DoCopy();
                        e.Use();
                        break;
                }
            }

            return requestRepaint;
        }

        private void MenuSelection(object userData, string[] options, int selected)
        {
            switch (selected)
            {
                // Copy
                case 0:
                    DoCopy();
                    break;
                // Select All
                case 1:
                    DoSelectAll();
                    break;
            }
        }
    }
}
