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
        private GUIContent kScroll = new GUIContent(L10n.Tr("Scroll:"), L10n.Tr("Disabled - No scrolling\nScroll To End - Always scroll to end\nAuto - switch to 'scroll to end' when scrolling to the last message"));
        class ScrollData
        {
            public int ScrollToItemWhileInDisabled { set; get; }
            public bool PerformScrollWhileInAuto { set; get; }

            public ScrollData()
            {
                Reset();
            }

            public void Reset()
            {
                ScrollToItemWhileInDisabled = -1;
                PerformScrollWhileInAuto = true;
            }
        }
        internal enum Column
        {
            Icon,
            Time,
            ProcessId,
            ThreadId,
            Priority,
            Tag,
            Message
        }

        private Vector2 m_ScrollPosition = Vector2.zero;
        private float m_MaxLogEntryWidth = 0.0f;
        private static readonly List<LogcatEntry> kNoEntries = new List<LogcatEntry>();
        private Dictionary<string, Priority> m_TagPriorityOnDevice = new Dictionary<string, Priority>();
        private float m_TagPriorityErrorHeight = 0.0f;

        public IReadOnlyList<LogcatEntry> FilteredEntries
        {
            get
            {
                if (m_Logcat == null)
                    return kNoEntries;
                return m_Logcat.FilteredEntries;
            }
        }

        public IReadOnlyList<LogcatEntry> GetSelectedFilteredEntries(out int minIndex, out int maxIndex)
        {
            minIndex = int.MaxValue;
            maxIndex = int.MinValue;
            if (m_Logcat == null)
                return kNoEntries;

            return m_Logcat.GetSelectedFilteredEntries(out minIndex, out maxIndex);
        }

        public IReadOnlyList<LogcatEntry> SelectedFilteredEntries => GetSelectedFilteredEntries(out var minIndex, out var maxIndex);

        private ScrollData m_ScrollData = new ScrollData();
        private float doubleClickStart = -1;

        void CollectTagPrioritiesFromDevice()
        {
            m_TagPriorityOnDevice.Clear();
            var d = m_Runtime.DeviceQuery.SelectedDevice;
            if (d == null && d.State != IAndroidLogcatDevice.DeviceState.Connected)
                return;

            var tags = m_Runtime.UserSettings.Tags.GetSelectedTags(true);
            if (tags == null || tags.Length == 0)
                tags = AndroidLogcatTags.DefaultTagNames;

            foreach (var tag in tags)
            {
                m_TagPriorityOnDevice[tag] = d.GetTagPriority(tag);
            }
        }

        private ColumnData[] Columns
        {
            get
            {
                return m_Runtime.Settings.ColumnData;
            }
        }

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

        private bool ShowColumn(Column column)
        {
            if (column == Column.Icon)
            {
                return m_Runtime.Settings.MessageFontSize > 11 && Columns[(int)column].enabled;
            }

            return Columns[(int)column].enabled;
        }

        private bool DoGUIHeader()
        {
            bool requestRepaint = false;
            var fullHeaderRect = GUILayoutUtility.GetRect(GUIContent.none, AndroidLogcatStyles.columnHeader, GUILayout.ExpandWidth(true));
            bool headerDrawn = false;
            bool lastHeaderDrawn = false;
            var offset = 0.0f;
            foreach (var c in (Column[])Enum.GetValues(typeof(Column)))
            {
                if (!ShowColumn(c))
                    continue;
                var d = Columns[(int)c];

                d.itemSize = new Rect(offset, fullHeaderRect.y, d.width, fullHeaderRect.height);
                offset += d.width;

                if (d.width > 0.0f)
                {
                    var buttonRect = d.itemSize;
                    buttonRect.x -= m_ScrollPosition.x;

                    switch ((Column)c)
                    {
                        case Column.Priority:
                            EditorGUI.BeginDisabledGroup(!IsLogcatConnected);
                            if (GUI.Button(buttonRect, d.content, AndroidLogcatStyles.columnHeader))
                            {
                                var priorities = (Priority[])Enum.GetValues(typeof(Priority));
                                EditorUtility.DisplayCustomMenu(new Rect(Event.current.mousePosition, Vector2.zero), priorities.Select(m => new GUIContent(m.ToString())).ToArray(), (int)m_Runtime.UserSettings.SelectedPriority, PrioritySelection, null);
                            }
                            EditorGUI.EndDisabledGroup();
                            break;
                        case Column.Tag:
                            EditorGUI.BeginDisabledGroup(!IsLogcatConnected);
                            if (GUI.Button(buttonRect, d.content, AndroidLogcatStyles.columnHeader))
                            {
                                m_Runtime.UserSettings.Tags.DoGUI(new Rect(Event.current.mousePosition, Vector2.zero), buttonRect);
                            }
                            EditorGUI.EndDisabledGroup();
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

                if (headerDrawn)
                {
                    // Don't allow splitter to make item small than 4px
                    // No need to do it for first visible item
                    d.itemSize.x = Mathf.Max(4.0f, d.itemSize.x);
                }
                headerDrawn = true;
            }

            if (!headerDrawn)
            {
                // If no header drawn, draw a empty label to the full header rect.
                GUI.Label(fullHeaderRect, GUIContent.none, AndroidLogcatStyles.columnHeader);
            }
            else if (!lastHeaderDrawn)
            {
                // If no last header drawn, draw an empty label to the remained header rect.
                float x = offset - m_ScrollPosition.x;
                var buttonRect = new Rect(x, fullHeaderRect.y, fullHeaderRect.width - x, fullHeaderRect.height);
                GUI.Label(buttonRect, GUIContent.none, AndroidLogcatStyles.columnHeader);
            }

            DoMouseEventsForHeaderToolbar(fullHeaderRect);
            return requestRepaint;
        }

        private bool DoGUITagsValidation()
        {
            if (!AndroidLogcatSessionSettings.ShowTagPriorityErrors)
                return false;

            var d = m_Runtime.DeviceQuery.SelectedDevice;
            if (d == null)
                return false;
            var result = false;
            var message = new StringBuilder();
            var fixCommand = new StringBuilder();
            foreach (var t in m_TagPriorityOnDevice)
            {
                if (t.Value == Priority.Verbose)
                    continue;
                message.AppendLine($" Tag '{t.Key}' has priority '{t.Value}'");
                fixCommand.AppendLine($"  adb shell setprop log.tag.{t.Key} {Priority.Verbose}");
            }

            // No tags to fix
            if (message.Length == 0)
                return false;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox($"Some tags on the device '{d.ShortDisplayName}' have invalid priorities, this can cause messages for these tags not to be displayed:\n{message}",
                MessageType.Error, true);
            if (Event.current.type == EventType.Repaint)
                m_TagPriorityErrorHeight = GUILayoutUtility.GetLastRect().height;

            EditorGUILayout.BeginVertical();
            var opts = new[] { GUILayout.Height(m_TagPriorityErrorHeight * 0.5f) };
            if (GUILayout.Button(new GUIContent("Fix Me", $"The following commands will be executed:\n{fixCommand}"), opts) && d != null)
            {
                foreach (var t in m_TagPriorityOnDevice)
                {
                    if (t.Value == Priority.Verbose)
                        continue;
                    d.SetTagPriority(t.Key, Priority.Verbose);
                    result = true;
                }
            }
            if (GUILayout.Button(new GUIContent("Hide", "Hide the error in this Editor session."), opts))
                AndroidLogcatSessionSettings.ShowTagPriorityErrors = false;
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            if (result)
                CollectTagPrioritiesFromDevice();
            return result;
        }

        private void MenuSelectionColumns(object userData, string[] options, int selected)
        {
            if (options[selected] == "Clear All")
            {
                foreach (var c in Columns)
                    c.enabled = false;
            }
            else if (options[selected] == "Select All")
            {
                foreach (var c in Columns)
                    c.enabled = true;
            }
            else if (selected < Columns.Length)
                Columns[selected].enabled = !Columns[selected].enabled;
        }

        private void PrioritySelection(object userData, string[] options, int selected)
        {
            SetSelectedPriority((Priority)selected);
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
                        for (int i = 0; i < Columns.Length; i++)
                        {
                            menuTexts.Add(((Column)i).ToString());
                            if (Columns[i].enabled)
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

        private void DoIconLogEntryItem(Rect fullView, int index, Column column, string value, GUIStyle style, Vector2 iconSize)
        {
            if (!ShowColumn(column))
                return;
            var itemRect = Columns[(uint)column].itemSize;
            var entryHeight = AndroidLogcatStyles.kLogEntryFixedHeight;
            var rc = new Rect(itemRect.x + (itemRect.width - iconSize.x) * 0.5f, fullView.y + entryHeight * index + (entryHeight - iconSize.y) * 0.5f, 0, 0);
            style.Draw(rc, new GUIContent(value), 0);
        }

        private void DoLogEntryItem(Rect fullView, int index, Column column, string value, GUIStyle style)
        {
            if (!ShowColumn(column))
                return;
            const float kMessageMargin = 5;
            var itemRect = Columns[(uint)column].itemSize;
            var rc = new Rect(itemRect.x + kMessageMargin, fullView.y + AndroidLogcatStyles.kLogEntryFixedHeight * index, itemRect.width - kMessageMargin, itemRect.height);
            style.Draw(rc, new GUIContent(value), 0);
        }

        private GUIStyle GetIconStyle(Priority priority)
        {
            switch (priority)
            {
                case Priority.Warn:
                    return AndroidLogcatStyles.warningSmallStyle;
                case Priority.Error:
                case Priority.Fatal:
                    return AndroidLogcatStyles.errorSmallStyle;
                default:
                    return AndroidLogcatStyles.infoSmallStyle;
            }
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
            totalWindowRect.height = AndroidLogcatStyles.kLogEntryFixedHeight * (FilteredEntries.Count + kExtraMessageCount);
            totalWindowRect.width = Mathf.Max(totalWindowRect.width, m_MaxLogEntryWidth);

            var controlId = GUIUtility.GetControlID(FocusType.Keyboard);

            var scalar = (float)(kExtraMessageCount + FilteredEntries.Count) / totalWindowRect.height;
            switch (m_Runtime.UserSettings.AutoScroll)
            {
                case AutoScroll.Disabled:
                    var scrollToItem = m_ScrollData.ScrollToItemWhileInDisabled;
                    if (scrollToItem >= 0)
                    {
                        // Do the scrolling in repain event since maxVisibleItems depends on visibleWindowRect
                        // And that one is calculated correctly only in Repaint event
                        if (e.type != EventType.Repaint)
                            break;

                        int minVisible = (int)(m_ScrollPosition.y * scalar);
                        int maxVisible = minVisible + maxVisibleItems;

                        // Only adjust scroll if our selected items is not among visible items
                        if (scrollToItem < minVisible)
                            m_ScrollPosition.y = scrollToItem / scalar;
                        if (scrollToItem > maxVisible - kExtraMessageCount)
                            m_ScrollPosition.y = (scrollToItem - maxVisibleItems + kExtraMessageCount) / scalar;

                        m_ScrollData.Reset();
                    }
                    break;
                case AutoScroll.ScrollToEnd:
                    m_ScrollPosition.y = totalWindowRect.height;
                    break;
                case AutoScroll.Auto:
                    if (m_ScrollData.PerformScrollWhileInAuto)
                        m_ScrollPosition.y = totalWindowRect.height;
                    break;
            }

            EditorGUI.BeginChangeCheck();
            m_ScrollPosition = GUI.BeginScrollView(visibleWindowRect, m_ScrollPosition, totalWindowRect, true, false);
            var startItem = (int)(m_ScrollPosition.y * scalar);

            // Check if we need to enable autoscrolling
            var scrollChanged = EditorGUI.EndChangeCheck();
            if (m_Runtime.UserSettings.AutoScroll == AutoScroll.Auto)
            {
                if (scrollChanged || (e.type == EventType.ScrollWheel && e.delta.y > 0.0f))
                    m_ScrollData.PerformScrollWhileInAuto = startItem + maxVisibleItems - kExtraMessageCount >= FilteredEntries.Count;
                else if (e.type == EventType.ScrollWheel && e.delta.y < 0.0f)
                    m_ScrollData.PerformScrollWhileInAuto = false;
            }

            if (e.type == EventType.Repaint)
            {
                // Max Log Entry width is used for calculating horizontal scrollbar
                m_MaxLogEntryWidth = 0.0f;
            }

            // Only draw items which can be visible on the screen
            // There can be thousands of log entries, drawing them all would kill performance
            for (int i = startItem; i - startItem < maxVisibleItems && i < FilteredEntries.Count; i++)
            {
                bool selected = FilteredEntries[i].Selected;
                var selectionRect = new Rect(visibleWindowRect.x, visibleWindowRect.y + AndroidLogcatStyles.kLogEntryFixedHeight * i, totalWindowRect.width, AndroidLogcatStyles.kLogEntryFixedHeight);

                if (e.type == EventType.Repaint)
                {
                    var le = FilteredEntries[i];
                    if (selected)
                        AndroidLogcatStyles.background.Draw(selectionRect, false, false, true, false);
                    else
                    {
                        if (i % 2 == 0)
                            AndroidLogcatStyles.backgroundEven.Draw(selectionRect, false, false, false, false);
                        else
                            AndroidLogcatStyles.backgroundOdd.Draw(selectionRect, false, false, false, false);
                    }
                    var style = AndroidLogcatStyles.priorityStyles[(int)le.priority];
                    DoIconLogEntryItem(visibleWindowRect, i, Column.Icon, "", GetIconStyle(le.priority), AndroidLogcatStyles.kSmallIconSize);
                    DoLogEntryItem(visibleWindowRect, i, Column.Time, le.dateTime.ToString(LogcatEntry.s_TimeFormat), style);
                    DoLogEntryItem(visibleWindowRect, i, Column.ProcessId, le.processId.ToString(), style);
                    DoLogEntryItem(visibleWindowRect, i, Column.ThreadId, le.threadId.ToString(), style);
                    DoLogEntryItem(visibleWindowRect, i, Column.Priority, le.priority.ToString(), style);
                    DoLogEntryItem(visibleWindowRect, i, Column.Tag, le.tag.ToString(), style);
                    DoLogEntryItem(visibleWindowRect, i, Column.Message, le.message, style);

                    m_MaxLogEntryWidth = Mathf.Max(m_MaxLogEntryWidth,
                        AndroidLogcatStyles.priorityDefaultStyle.CalcSize(new GUIContent(le.message)).x + Columns[(int)Column.Message].itemSize.x);
                }
                else
                {
                    requestRepaint |= DoMouseEventsForLogEntry(selectionRect, i, selected, controlId);
                }
            }

            requestRepaint |= DoKeyEvents(controlId);

            GUI.EndScrollView();

            Rect rc = GUILayoutUtility.GetLastRect();
            // Decrement horizontal scrollbar height
            rc.height -= 15.0f;
            DoColumnBorders(rc, Color.black, 1);

            return requestRepaint;
        }

        private void DoColumnBorders(Rect visibleWindowRect, Color borderColor, float borderWidth)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            var orgColor = GUI.color;
            GUI.color = borderColor;
            for (int i = 0; i < Enum.GetValues(typeof(Column)).Length; i++)
            {
                if (!ShowColumn((Column)i))
                    continue;
                var itemRect = Columns[i].itemSize;
                var rc = new Rect(itemRect.x + itemRect.width - m_ScrollPosition.x, visibleWindowRect.y, borderWidth, visibleWindowRect.height);
                GUI.DrawTexture(rc, EditorGUIUtility.whiteTexture);
            }

            GUI.color = orgColor;
        }


        private void DoMouseSelection(Event e, int logEntryIndex, bool isLogEntrySelected, int keyboardControlId)
        {
            var entry = FilteredEntries[logEntryIndex];
            var selectedEntries = GetSelectedFilteredEntries(out var minSelectedIndex, out var maxSelectedIndex);
            if (e.HasCtrlOrCmdModifier())
            {
                entry.Selected = !entry.Selected;
            }
            else if ((e.modifiers & EventModifiers.Shift) != 0)
            {
                if (selectedEntries.Count == 0)
                {
                    entry.Selected = true;
                }
                else
                {
                    if (logEntryIndex < minSelectedIndex)
                        minSelectedIndex = logEntryIndex;
                    if (logEntryIndex > maxSelectedIndex)
                        maxSelectedIndex = logEntryIndex;
                    for (int si = minSelectedIndex; si <= maxSelectedIndex; si++)
                    {
                        FilteredEntries[si].Selected = true;
                    }
                }
            }
            else
            {
                if (isLogEntrySelected && selectedEntries.Count == 1)
                {
                    if ((Time.realtimeSinceStartup - doubleClickStart) < 0.3f)
                        TryToOpenFileFromLogEntry(FilteredEntries[logEntryIndex]);
                    doubleClickStart = -1;
                }
                else
                {
                    // Curious behavior with right click. In Unity if you right click on already selected item which is a part of selection list, it doesn't deselect other items
                    // But if you right click on unselected item, the selection list will be cleared
                    if (e.button == 0 ||
                        (e.button == 1 && !entry.Selected))
                    {
                        m_Logcat?.ClearSelectedEntries();
                        entry.Selected = true;
                    }
                    doubleClickStart = Time.realtimeSinceStartup;
                }
            }

            GUIUtility.keyboardControl = keyboardControlId;
        }

        public class ContextMenuUserData
        {
            public LogcatEntry[] SelectedEntries;
            public LogcatEntry TagProcessIdEntry;
        }

        void DoContextMenu(Event e, int logEntryIndex)
        {
            var entries = SelectedFilteredEntries;
            var contextMenu = new AndroidContextMenu<MessagesContextMenu>();
            contextMenu.Add(MessagesContextMenu.Copy, "Copy");
            contextMenu.Add(MessagesContextMenu.SelectAll, "Select All");
            contextMenu.AddSplitter();
            contextMenu.Add(MessagesContextMenu.SaveSelection, "Save Selection...");

            var userData = new ContextMenuUserData();
            userData.SelectedEntries = entries.ToArray();
            if (entries.Count > 0)
            {
                userData.TagProcessIdEntry = FilteredEntries[logEntryIndex];
                var tag = userData.TagProcessIdEntry.tag;
                if (!string.IsNullOrEmpty(tag))
                {
                    contextMenu.AddSplitter();

                    var fixedTag = AndroidLogcatUtilities.FixSlashesForIMGUI(tag);
                    contextMenu.Add(MessagesContextMenu.AddTag, $"Add tag '{fixedTag}'", false, IsLogcatConnected);
                    contextMenu.Add(MessagesContextMenu.RemoveTag, $"Remove tag '{fixedTag}'", false, IsLogcatConnected);
                }

                var processId = userData.TagProcessIdEntry.processId;
                if (processId >= 0)
                {
                    contextMenu.AddSplitter();
                    contextMenu.Add(MessagesContextMenu.FilterByProcessId, $"Filter by process id '{processId}'", false, IsLogcatConnected);

                    contextMenu.AddSplitter();

                    var prefix = $"Process Manager (pid = '{processId}')/";
                    foreach (var signal in (PosixSignal[])Enum.GetValues(typeof(PosixSignal)))
                    {
                        contextMenu.Add(MessagesContextMenu.SendUnixSignal, $"{prefix}Send Unix signal/{signal} ({(int)signal})", false, IsLogcatConnected,
                            new KeyValuePair<int, PosixSignal>(processId, signal));
                    }

                    contextMenu.Add(MessagesContextMenu.CrashProcess, $"{prefix}Crash", false, IsLogcatConnected, processId);
                    contextMenu.Add(MessagesContextMenu.ForceStop, $"{prefix}Force Stop", false, IsLogcatConnected, processId);
                    contextMenu.Add(default, prefix);
                    foreach (var usage in AndroidLogcatSendTrimMemoryUsage.All)
                    {
                        contextMenu.Add(MessagesContextMenu.SendTrimMemory, $"{prefix}Send Trim Memory/{usage.DisplayName}", false, IsLogcatConnected,
                            new KeyValuePair<int, AndroidLogcatSendTrimMemoryUsage>(processId, usage));
                    }
                }
            }
            else
            {
                userData.TagProcessIdEntry = null;
            }

            contextMenu.UserData = userData;
            contextMenu.Show(e.mousePosition, MenuSelection);
        }

        private void MenuSelection(object userData, string[] options, int selected)
        {
            var contextMenu = (AndroidContextMenu<MessagesContextMenu>)userData;
            var contextMenuUserData = ((ContextMenuUserData)contextMenu.UserData);
            var entries = contextMenuUserData.SelectedEntries;

            var item = contextMenu.GetItemAt(selected);
            if (item == null)
                return;

            switch (item.Item)
            {
                // Copy
                case MessagesContextMenu.Copy:
                    EditorGUIUtility.systemCopyBuffer = LogEntriesToString(entries);
                    break;
                // Select All
                case MessagesContextMenu.SelectAll:
                    m_Logcat?.SelectAllFilteredEntries();
                    break;
                // Save to File
                case MessagesContextMenu.SaveSelection:
                    SaveToFile(entries);
                    break;
                // Add tag
                case MessagesContextMenu.AddTag:
                    AddTag(contextMenuUserData.TagProcessIdEntry.tag);
                    break;
                // Remove tag
                case MessagesContextMenu.RemoveTag:
                    RemoveTag(contextMenuUserData.TagProcessIdEntry.tag);
                    break;
                // Filter by process id
                case MessagesContextMenu.FilterByProcessId:
                    FilterByProcessId(contextMenuUserData.TagProcessIdEntry.processId);
                    break;
                case MessagesContextMenu.SendUnixSignal:
                    {
                        var data = (KeyValuePair<int, PosixSignal>)item.UserData;
                        m_Runtime.DeviceQuery.SelectedDevice.KillProcess(data.Key, data.Value);
                    }
                    break;
                case MessagesContextMenu.CrashProcess:
                    m_Runtime.DeviceQuery.SelectedDevice.ActivityManager.CrashProcess((int)item.UserData);
                    break;
                case MessagesContextMenu.ForceStop:
                    m_Runtime.DeviceQuery.SelectedDevice.ActivityManager.StopProcess((int)item.UserData);
                    break;
                case MessagesContextMenu.SendTrimMemory:
                    {
                        var data = (KeyValuePair<int, AndroidLogcatSendTrimMemoryUsage>)item.UserData;
                        m_Runtime.DeviceQuery.SelectedDevice.ActivityManager.SendTrimMemory(data.Key, data.Value);
                    }
                    break;
            }
        }

        private bool DoMouseEventsForLogEntry(Rect logEntryRect, int logEntryIndex, bool isLogEntrySelected, int keyboardControlId)
        {
            bool requestRepaint = false;
            var e = Event.current;
            if (e.type == EventType.MouseDown && logEntryRect.Contains(e.mousePosition))
            {
                // Selection occurs both with Left Click & and Right click, this happens in all Unity windows.
                if (e.button == 0 || e.button == 1)
                {
                    DoMouseSelection(e, logEntryIndex, isLogEntrySelected, keyboardControlId);

                    requestRepaint = true;
                    e.Use();
                }
            }

            if (e.type == EventType.MouseUp && logEntryRect.Contains(e.mousePosition))
            {
                if (e.button == 1)
                {
                    DoContextMenu(e, logEntryIndex);
                    requestRepaint = true;
                    e.Use();
                }
            }

            return requestRepaint;
        }

        private void DoNavigation(int direction, bool shiftPressed)
        {
            if (m_Logcat == null)
                return;
            if (m_Logcat.FilteredEntries.Count == 0)
                return;
            var selectedEntries = GetSelectedFilteredEntries(out var minIdx, out var maxIdx);
            var selectedItem = 0;
            switch (direction)
            {
                case -1:
                    selectedItem = selectedEntries.Count == 0 ? m_Logcat.FilteredEntries.Count - 1 : Math.Max(0, minIdx - 1);
                    break;
                case 1:
                    selectedItem = selectedEntries.Count == 0 ? 0 : Math.Min(m_Logcat.FilteredEntries.Count - 1, maxIdx + 1);
                    break;
                default:
                    throw new NotImplementedException($"Unsupported navigation: {direction}");
            }
            if (shiftPressed)
            {
                for (int i = minIdx; i <= maxIdx; i++)
                    m_Logcat.FilteredEntries[i].Selected = true;
            }
            else
            {
                m_Logcat.ClearSelectedEntries();
            }

            m_Logcat.FilteredEntries[selectedItem].Selected = true;
            m_Runtime.UserSettings.AutoScroll = AutoScroll.Disabled;
            m_ScrollData.ScrollToItemWhileInDisabled = selectedItem;
        }

        private void DoScrollOptionsGUI()
        {
            GUILayout.Label(kScroll, GUILayout.ExpandWidth(false));
            EditorGUI.BeginChangeCheck();
            var scroll = (AutoScroll)EditorGUILayout.EnumPopup(m_Runtime.UserSettings.AutoScroll, GUILayout.ExpandWidth(false));
            if (EditorGUI.EndChangeCheck())
            {
                m_ScrollData.Reset();
                m_Runtime.UserSettings.AutoScroll = scroll;
            }
        }

        private bool DoKeyEvents(int controlId)
        {
            if (GUIUtility.keyboardControl != controlId)
                return false;

            var requestRepaint = false;
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                bool hasCtrlOrCmd = e.HasCtrlOrCmdModifier();
                bool hasShift = (e.modifiers & EventModifiers.Shift) != 0;
                switch (e.keyCode)
                {
                    case KeyCode.A:
                        if (hasCtrlOrCmd)
                        {
                            m_Logcat?.SelectAllFilteredEntries();
                            e.Use();
                            requestRepaint = true;
                        }
                        break;
                    case KeyCode.C:
                        if (hasCtrlOrCmd)
                        {
                            EditorGUIUtility.systemCopyBuffer = LogEntriesToString(SelectedFilteredEntries);
                            e.Use();
                        }
                        break;
                    case KeyCode.S:
                        if (hasCtrlOrCmd)
                        {
                            SaveToFile(SelectedFilteredEntries);
                            e.Use();
                        }
                        break;
                    case KeyCode.DownArrow:
                        DoNavigation(1, hasShift);
                        requestRepaint = true;
                        e.Use();
                        break;
                    case KeyCode.UpArrow:
                        DoNavigation(-1, hasShift);
                        requestRepaint = true;
                        e.Use();
                        break;
                    default:
                        break;
                }
            }

            return requestRepaint;
        }

        public bool DoMessageView()
        {
            var repaint = DoGUIHeader();
            repaint |= DoGUITagsValidation();
            repaint |= DoGUIEntries();
            return repaint;
        }

        private void SaveToFile(IEnumerable<LogcatEntry> logEntries)
        {
            var contents = LogEntriesToString(logEntries);
            var filePath = EditorUtility.SaveFilePanel("Save selected logs", "", PlayerSettings.applicationIdentifier + "-logcat", "txt");
            if (!string.IsNullOrEmpty(filePath))
                File.WriteAllText(filePath, contents);
        }

        private string LogEntriesToString(IEnumerable<LogcatEntry> entries)
        {
            var contents = new StringBuilder();
            foreach (var l in entries)
            {
                var entry = string.Empty;
                for (int i = 0; i < Columns.Length; i++)
                {
                    if (!ShowColumn((Column)i))
                        continue;
                    if (entry.Length > 0)
                        entry += " ";
                    switch ((Column)i)
                    {
                        case Column.Time: entry += l.dateTime.ToString(LogcatEntry.s_TimeFormat); break;
                        case Column.ProcessId: entry += l.processId; break;
                        case Column.ThreadId: entry += l.threadId; break;
                        case Column.Priority: entry += l.priority; break;
                        case Column.Tag: entry += l.tag; break;
                        case Column.Message: entry += l.message; break;
                    }
                }
                contents.AppendLine(entry);
            }

            return contents.ToString();
        }

        private void TryToOpenFileFromLogEntry(LogcatEntry entry)
        {
            Regex re = new Regex(@"at.*\s([^\s]+):(\d+)");
            var match = re.Match(entry.message);
            if (match.Success)
                UnityEditorInternal.InternalEditorUtility.TryOpenErrorFileFromConsole(match.Groups[1].Value, Int32.Parse(match.Groups[2].Value));
        }
    }
}
