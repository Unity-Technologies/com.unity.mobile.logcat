#if PLATFORM_ANDROID
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Unity.Android.Logcat
{
    internal partial class AndroidLogcatConsoleWindow
    {
        internal enum Column
        {
            Time,
            ProcessId,
            ThreadId,
            Priority,
            Tag,
            Message
        }

        [Serializable]
        public class ColumnData
        {
            [NonSerialized]
            public GUIContent content;

            public float width;

            [NonSerialized]
            // Updated automatically when we're moving the splitter
            public Rect itemSize = Rect.zero;

            [NonSerialized]
            public bool splitterDragging;

            [NonSerialized]
            public float splitterDragStartMouseValue;

            [NonSerialized]
            public float splitterDragStartWidthValue;

            public bool enabled = true;
        }

        private List<int> m_SelectedIndices = new List<int>();
        private Vector2 m_ScrollPosition = Vector2.zero;
        private float m_MaxLogEntryWidth = 0.0f;

        [SerializeField]
        private ColumnData[] m_Columns = new ColumnData[]
        {
            new ColumnData() {content = EditorGUIUtility.TrTextContent("Time", "Time when event occured"), width = 160.0f },
            new ColumnData() {content = EditorGUIUtility.TrTextContent("Pid", "Process Id"), width = 50.0f  },
            new ColumnData() {content = EditorGUIUtility.TrTextContent("Tid", "Thread Id"), width = 50.0f  },
            new ColumnData() {content = EditorGUIUtility.TrTextContent("Priority", "Priority (Left click to select different priorities)"), width = 50.0f  },
            new ColumnData() {content = EditorGUIUtility.TrTextContent("Tag", "Tag (Left click to select different tags)"), width = 50.0f  },
            new ColumnData() {content = EditorGUIUtility.TrTextContent("Message", ""), width = -1  },
        };

        private bool m_Autoscroll = true;
        private float doubleClickStart = -1;

        private bool DoSplitter(ColumnData data, Rect splitterRect)
        {
            const float kSplitterWidth = 3.0f;
            splitterRect.x = splitterRect.x + splitterRect.width - kSplitterWidth * 0.5f;
            splitterRect.width = kSplitterWidth;


            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);
            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (splitterRect.Contains(e.mousePosition))
                    {
                        data.splitterDragging = true;
                        data.splitterDragStartMouseValue = e.mousePosition.x;
                        data.splitterDragStartWidthValue = data.width;
                        e.Use();
                        return true;
                    }
                    break;
                case EventType.MouseDrag:
                case EventType.MouseUp:
                    if (data.splitterDragging)
                    {
                        data.width = Mathf.Max(20.0f, data.splitterDragStartWidthValue + e.mousePosition.x - data.splitterDragStartMouseValue);

                        if (e.type == EventType.MouseUp)
                        {
                            data.splitterDragging = false;
                        }
                        e.Use();
                        return true;
                    }
                    break;
            }

            return false;
        }

        private bool DoGUIHeader()
        {
            bool requestRepaint = false;
            var fullHeaderRect = GUILayoutUtility.GetRect(GUIContent.none, AndroidLogcatStyles.columnHeader, GUILayout.ExpandWidth(true));
            bool headerDrawn = false;
            bool lastHeaderDrawn = false;
            var offset = 0.0f;
            foreach (var c in Enum.GetValues(typeof(Column)))
            {
                var d = m_Columns[(int)c];
                if (!d.enabled)
                    continue;

                d.itemSize = new Rect(offset, fullHeaderRect.y, d.width, fullHeaderRect.height);
                offset += d.width;

                if (d.width > 0.0f)
                {
                    var buttonRect = d.itemSize;
                    buttonRect.x -= m_ScrollPosition.x;

                    switch ((Column)c)
                    {
                        case Column.Priority:
                            if (GUI.Button(buttonRect, d.content, AndroidLogcatStyles.columnHeader))
                            {
                                var priorities = (AndroidLogcat.Priority[])Enum.GetValues(typeof(AndroidLogcat.Priority));
                                EditorUtility.DisplayCustomMenu(new Rect(Event.current.mousePosition, Vector2.zero), priorities.Select(m => new GUIContent(m.ToString())).ToArray(), (int)m_SelectedPriority, PrioritySelection, null);
                            }
                            break;
                        case Column.Tag:
                            if (GUI.Button(buttonRect, d.content, AndroidLogcatStyles.columnHeader))
                            {
                                m_TagControl.DoGUI(new Rect(Event.current.mousePosition, Vector2.zero));
                            }
                            break;
                        default:
                            GUI.Label(buttonRect, d.content, AndroidLogcatStyles.columnHeader);
                            break;
                    }

                    requestRepaint |= DoSplitter(d, buttonRect);
                }
                else
                {
                    var buttonRect = d.itemSize;
                    buttonRect.x -= m_ScrollPosition.x;
                    buttonRect.width = fullHeaderRect.width - offset + m_ScrollPosition.x;

                    GUI.Label(buttonRect, d.content, AndroidLogcatStyles.columnHeader);
                    // For last entry have a really big width, so all the message can fit
                    d.itemSize.width = 10000.0f;
                    lastHeaderDrawn = true;
                }

                // Don't allow splitter to make item small than 4px
                d.itemSize.x = Mathf.Max(4.0f, d.itemSize.x);
                headerDrawn = true;
            }

            if (!headerDrawn || !lastHeaderDrawn)
                GUI.Label(fullHeaderRect, GUIContent.none, AndroidLogcatStyles.columnHeader);
            DoMouseEventsForHeaderToolbar(fullHeaderRect);
            return requestRepaint;
        }

        private void MenuSelectionColumns(object userData, string[] options, int selected)
        {
            if (options[selected] == "Clear All")
            {
                foreach (var c in m_Columns)
                    c.enabled = false;
            }
            else if (options[selected] == "Select All")
            {
                foreach (var c in m_Columns)
                    c.enabled = true;
            }
            else if (selected < m_Columns.Length)
                m_Columns[selected].enabled = !m_Columns[selected].enabled;
        }

        private void PrioritySelection(object userData, string[] options, int selected)
        {
            SetSelectedPriority((AndroidLogcat.Priority)selected);
        }

        private void DoMouseEventsForHeaderToolbar(Rect headerRect)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && headerRect.Contains(e.mousePosition))
            {
                switch (e.button)
                {
                    case 1:
                        var menuTexts = new List<string>();
                        var menuSelected = new List<int>();
                        for (int i = 0; i < m_Columns.Length; i++)
                        {
                            menuTexts.Add(((Column)i).ToString());
                            if (m_Columns[i].enabled)
                                menuSelected.Add(i);
                        }

                        menuTexts.Add("");
                        menuTexts.Add("Clear All");
                        menuTexts.Add("Select All");
                        e.Use();

                        var enabled = Enumerable.Repeat(true, menuTexts.Count).ToArray();
                        var separator = new bool[menuTexts.Count];
                        EditorUtility.DisplayCustomMenuWithSeparators(new Rect(e.mousePosition.x, e.mousePosition.y, 0, 0),
                            menuTexts.ToArray(),
                            enabled,
                            separator,
                            menuSelected.ToArray(),
                            MenuSelectionColumns,
                            null);
                        break;
                }
            }
        }

        private void DoLogEntryItem(Rect fullView, int index, Column column, string value, GUIStyle style)
        {
            if (!m_Columns[(int)column].enabled)
                return;
            var itemRect = m_Columns[(uint)column].itemSize;
            var rc = new Rect(itemRect.x, fullView.y + AndroidLogcatStyles.kLogEntryFixedHeight * index, itemRect.width, itemRect.height);
            style.Draw(rc, new GUIContent(value), 0);
        }

        private bool DoGUIEntries()
        {
            bool requestRepaint = false;
            var e = Event.current;

            var visibleWindowRect = GUILayoutUtility.GetRect(GUIContent.none, AndroidLogcatStyles.priorityDefaultStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            var totalWindowRect = visibleWindowRect;
            var maxVisibleItems = (int)(visibleWindowRect.height / AndroidLogcatStyles.kLogEntryFixedHeight);
            // Extra message count ensures that there's an empty space below when we scrolling all the way down
            // This way it's easier to see that there's no more messages
            const int kExtraMessageCount = 5;
            totalWindowRect.height = AndroidLogcatStyles.kLogEntryFixedHeight * (m_LogEntries.Count + kExtraMessageCount);
            totalWindowRect.width = Mathf.Max(totalWindowRect.width, m_MaxLogEntryWidth);

            if (m_Autoscroll)
                m_ScrollPosition.y = totalWindowRect.height;

            EditorGUI.BeginChangeCheck();
            m_ScrollPosition = GUI.BeginScrollView(visibleWindowRect, m_ScrollPosition, totalWindowRect);
            int startItem = (int)(m_ScrollPosition.y / totalWindowRect.height * (kExtraMessageCount + m_LogEntries.Count));

            // Check if we need to enable autoscrolling
            if (EditorGUI.EndChangeCheck() || (e.type == EventType.ScrollWheel && e.delta.y > 0.0f))
                m_Autoscroll = startItem + maxVisibleItems - kExtraMessageCount >= m_LogEntries.Count;
            else if (e.type == EventType.ScrollWheel && e.delta.y < 0.0f)
                m_Autoscroll = false;

            if (e.type == EventType.Repaint)
            {
                // Max Log Entry width is used for calculating horizontal scrollbar
                m_MaxLogEntryWidth = 0.0f;
            }

            // Only draw items which can be visible on the screen
            // There can be thousands of log entries, drawing them all would kill performance
            for (int i = startItem; i - startItem < maxVisibleItems && i < m_LogEntries.Count; i++)
            {
                bool selected = m_SelectedIndices.Contains(i);
                var selectionRect = new Rect(visibleWindowRect.x, visibleWindowRect.y + AndroidLogcatStyles.kLogEntryFixedHeight * i, totalWindowRect.width, AndroidLogcatStyles.kFixedHeight);

                if (e.type == EventType.Repaint)
                {
                    var le = m_LogEntries[i];
                    if (selected)
                        AndroidLogcatStyles.background.Draw(selectionRect, false, false, true, false);
                    var style = AndroidLogcatStyles.priorityStyles[(int)le.priority];
                    DoLogEntryItem(visibleWindowRect, i, Column.Time, le.dateTime.ToString(AndroidLogcat.LogEntry.s_TimeFormat), style);
                    DoLogEntryItem(visibleWindowRect, i, Column.ProcessId, le.processId.ToString(), style);
                    DoLogEntryItem(visibleWindowRect, i, Column.ThreadId, le.threadId.ToString(), style);
                    DoLogEntryItem(visibleWindowRect, i, Column.Priority, le.priority.ToString(), style);
                    DoLogEntryItem(visibleWindowRect, i, Column.Tag, le.tag.ToString(), style);
                    DoLogEntryItem(visibleWindowRect, i, Column.Message, le.message, style);

                    m_MaxLogEntryWidth = Mathf.Max(m_MaxLogEntryWidth,
                        AndroidLogcatStyles.priorityDefaultStyle.CalcSize(new GUIContent(le.message)).x + m_Columns[(int)Column.Message].itemSize.x);
                }
                else
                {
                    requestRepaint |= DoMouseEventsForLogEntry(selectionRect, i, selected);
                }
            }

            requestRepaint |= DoKeyEvents();

            GUI.EndScrollView();

            return requestRepaint;
        }

        private static bool HasCtrlOrCmdModifier(Event e)
        {
            return (e.modifiers & (Application.platform == RuntimePlatform.OSXEditor ? EventModifiers.Command : EventModifiers.Control)) != 0;
        }

        private bool DoMouseEventsForLogEntry(Rect logEntryRect, int logEntryIndex, bool isLogEntrySelected)
        {
            bool requestRepaint = false;
            var e = Event.current;
            if (e.type == EventType.MouseDown && logEntryRect.Contains(e.mousePosition))
            {
                switch (e.button)
                {
                    case 0:
                        if (HasCtrlOrCmdModifier(e))
                        {
                            if (m_SelectedIndices.Contains(logEntryIndex))
                                m_SelectedIndices.Remove(logEntryIndex);
                            else
                                m_SelectedIndices.Add(logEntryIndex);
                        }
                        else if ((e.modifiers & EventModifiers.Shift) != 0)
                        {
                            if (m_SelectedIndices.Count == 0)
                            {
                                m_SelectedIndices.Add(logEntryIndex);
                            }
                            else
                            {
                                int minValue = logEntryIndex;
                                int maxValue = logEntryIndex;
                                foreach (var si in m_SelectedIndices)
                                {
                                    if (si > maxValue)
                                        maxValue = si;
                                    else if (si < minValue)
                                        minValue = si;
                                }

                                for (int si = minValue; si <= maxValue; si++)
                                {
                                    if (m_SelectedIndices.Contains(si))
                                        continue;
                                    m_SelectedIndices.Add(si);
                                }
                            }
                        }
                        else
                        {
                            if (isLogEntrySelected && m_SelectedIndices.Count == 1)
                            {
                                if ((Time.realtimeSinceStartup - doubleClickStart) < 0.3f)
                                    TryToOpenFileFromLogEntry(m_LogEntries[logEntryIndex]);
                                else
                                    m_SelectedIndices.Remove(logEntryIndex);
                                doubleClickStart = -1;
                            }
                            else
                            {
                                m_SelectedIndices.Clear();
                                m_SelectedIndices.Add(logEntryIndex);
                                doubleClickStart = Time.realtimeSinceStartup;
                            }
                        }

                        m_SelectedIndices.Sort();
                        e.Use();
                        requestRepaint = true;
                        break;
                    case 1:
                        var entries = new List<AndroidLogcat.LogEntry>();
                        foreach (var si in m_SelectedIndices)
                        {
                            if (si > m_LogEntries.Count - 1)
                                continue;
                            entries.Add(m_LogEntries[si]);
                        }
                        var menuItems = new List<string>();
                        menuItems.AddRange(new[] { "Copy", "Select All", "", "Save Selection..." });

                        if (entries.Count > 0)
                        {
                            menuItems.Add("");
                            menuItems.Add("Clear tags");
                            menuItems.Add("Add tag '" + entries[0].tag + "'");
                            menuItems.Add("Remove tag '" + entries[0].tag + "'");
                        }

                        var enabled = Enumerable.Repeat(true, menuItems.Count).ToArray();
                        var separator = new bool[menuItems.Count];
                        EditorUtility.DisplayCustomMenuWithSeparators(new Rect(e.mousePosition.x, e.mousePosition.y, 0, 0),
                            menuItems.ToArray(),
                            enabled,
                            separator,
                            null,
                            MenuSelection,
                            entries.ToArray());
                        break;
                }
            }
            return requestRepaint;
        }

        private bool DoKeyEvents()
        {
            var requestRepaint = false;
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                bool hasCtrlOrCmd = HasCtrlOrCmdModifier(e);
                switch (e.keyCode)
                {
                    case KeyCode.A:
                        if (hasCtrlOrCmd)
                        {
                            SelectAll();
                            e.Use();
                            requestRepaint = true;
                        }
                        break;
                    case KeyCode.C:
                        if (hasCtrlOrCmd)
                        {
                            var copyText = new StringBuilder();
                            foreach (var si in m_SelectedIndices)
                            {
                                if (si >= m_LogEntries.Count)
                                    continue;
                                copyText.AppendLine(m_LogEntries[si].ToString());
                            }
                            EditorGUIUtility.systemCopyBuffer = copyText.ToString();
                            e.Use();
                        }
                        break;
                    case KeyCode.S:
                        if (hasCtrlOrCmd)
                        {
                            var logEntries = new List<AndroidLogcat.LogEntry>();
                            foreach (var si in m_SelectedIndices)
                            {
                                if (si > m_LogEntries.Count - 1)
                                    continue;
                                logEntries.Add(m_LogEntries[si]);
                            }
                            SaveToFile(logEntries.ToArray());
                            e.Use();
                        }
                        break;
                    default:
                        break;
                }
            }

            return requestRepaint;
        }

        public bool DoMessageView()
        {
            return DoGUIHeader() | DoGUIEntries();
        }

        private void SelectAll()
        {
            m_SelectedIndices.Clear();
            for (int si = 0; si < m_LogEntries.Count; si++)
                m_SelectedIndices.Add(si);
        }

        private void SaveToFile(AndroidLogcat.LogEntry[] logEntries)
        {
            var contents = new StringBuilder();
            foreach (var l in logEntries)
            {
                var entry = string.Empty;
                for (int i = 0; i < m_Columns.Length; i++)
                {
                    if (!m_Columns[i].enabled)
                        continue;
                    if (entry.Length > 0)
                        entry += " ";
                    switch ((Column)i)
                    {
                        case Column.Time: entry += l.dateTime.ToString(AndroidLogcat.LogEntry.s_TimeFormat); break;
                        case Column.ProcessId: entry += l.processId; break;
                        case Column.ThreadId: entry += l.threadId; break;
                        case Column.Priority: entry += l.priority; break;
                        case Column.Tag: entry += l.tag; break;
                        case Column.Message: entry += l.message; break;
                    }
                }
                contents.AppendLine(entry);
            }
            var filePath = EditorUtility.SaveFilePanel("Save selected logs", "", PlayerSettings.applicationIdentifier + "-logcat", "txt");
            if (!string.IsNullOrEmpty(filePath))
                File.WriteAllText(filePath, contents.ToString());
        }

        private void MenuSelection(object userData, string[] options, int selected)
        {
            switch (selected)
            {
                // Copy
                case 0:
                    var selectedLogEntries = (AndroidLogcat.LogEntry[])userData;
                    var text = new StringBuilder();
                    foreach (var l in selectedLogEntries)
                        text.AppendLine(l.ToString());
                    EditorGUIUtility.systemCopyBuffer = text.ToString();
                    break;
                // Select All
                case 1:
                    SelectAll();
                    break;
                // Save to File
                case 3:
                    SaveToFile((AndroidLogcat.LogEntry[])userData);
                    break;
                // Clear tags
                case 5:
                    ClearTags();
                    break;
                // Add tag
                case 6:
                    AddTag(((AndroidLogcat.LogEntry[])userData)[0].tag);
                    break;
                // Remove tag
                case 7:
                    RemoveTag(((AndroidLogcat.LogEntry[])userData)[0].tag);
                    break;
            }
        }

        private void TryToOpenFileFromLogEntry(AndroidLogcat.LogEntry entry)
        {
            Regex re = new Regex(@"at.*\s([^\s]+):(\d+)");
            var match = re.Match(entry.message);
            if (match.Success)
                UnityEditorInternal.InternalEditorUtility.TryOpenErrorFileFromConsole(match.Groups[1].Value, Int32.Parse(match.Groups[2].Value));
        }
    }
}
#endif
