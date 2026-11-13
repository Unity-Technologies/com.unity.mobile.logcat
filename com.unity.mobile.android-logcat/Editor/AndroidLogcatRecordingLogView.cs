using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// The logcat messages saved with a recording, in a panel under its video. A message
    /// belongs to the first frame captured after it was logged: the shown frame's are
    /// highlighted, and later ones are dimmed, as the video has not reached them yet.
    /// </summary>
    internal class AndroidLogcatRecordingLogView
    {
        [NoAutoStaticsCleanup]
        static class Styles
        {
            internal static readonly GUIContent Title = new GUIContent("Logcat Messages",
                "Logged while recording, filtered as the Android Logcat window was when recording started. " +
                "Video is the time of the frame each message belongs to. " +
                "Highlighted: logged since the previous frame. Click a message to go to its frame.");
            internal static readonly GUIContent Video = new GUIContent("Video", "The time into the video of the frame the message belongs to.");
        }

        // The Logcat window's columns, with the video time first. The shared ones take
        // their width and visibility from the Logcat window's settings, so both line up.
        enum Column
        {
            Icon,
            Video,
            Time,
            ProcessId,
            ThreadId,
            Priority,
            Tag,
            Message
        }

        const float kSplitterHeight = 5;
        const float kMinHeight = 60;
        const float kMinVideoHeight = 100;
        const float kDefaultHeight = 160;
        const float kVideoTimeWidth = 60;
        // As the Logcat window indents a column's text.
        const float kTextMargin = 5;
        const float kScrollbarWidth = 16;
        // Rows after the shown frame, which the video has not reached.
        const float kFutureAlpha = 0.45f;

        // The Logcat window's message styles with the font and colours from Preferences,
        // which only that window applies to its own.
        readonly GUIStyle[] m_PriorityStyles = new GUIStyle[Enum.GetValues(typeof(Priority)).Length];
        readonly float[] m_ColumnX = new float[Enum.GetValues(typeof(Column)).Length];
        readonly float[] m_ColumnWidth = new float[Enum.GetValues(typeof(Column)).Length];
        float m_RowHeight;

        readonly LogcatEntry[] m_Entries;
        // The time of the frame each message belongs to, in order.
        readonly double[] m_FrameTimes;
        // With minutes, as the player shows a recording a minute or longer.
        readonly bool m_Minutes;
        readonly AndroidLogcatRuntimeBase m_Runtime;
        Vector2 m_Scroll;
        float m_Height = kDefaultHeight;
        bool m_Dragging;
        // The frame the list last scrolled for, so it follows the video without
        // fighting a scroll made while paused.
        double m_ScrolledFor = double.NaN;

        internal bool HasMessages => m_Entries.Length > 0;

        internal AndroidLogcatRecordingLogView(AndroidLogcatRuntimeBase runtime, AndroidLogcatCaptureInfo.FrameChange[] changes)
        {
            m_Runtime = runtime;
            var rows = (changes ?? Array.Empty<AndroidLogcatCaptureInfo.FrameChange>())
                .Where(change => change?.logs != null)
                .OrderBy(change => change.time)
                .SelectMany(change => change.logs.Select(entry => (change.time, entry)))
                .ToArray();
            m_Entries = rows.Select(row => row.entry).ToArray();
            m_FrameTimes = rows.Select(row => row.time).ToArray();
            m_Minutes = m_FrameTimes.Length > 0 && m_FrameTimes[m_FrameTimes.Length - 1] >= 60;
        }

        /// <summary>
        /// Takes the bottom of <paramref name="area"/> and draws the messages there, as of
        /// the frame at <paramref name="time"/>. Returns what is left above for the video.
        /// </summary>
        /// <param name="seek">Goes to the frame at a time, for a clicked message.</param>
        internal Rect DoGUI(Rect area, double time, double frameDuration, Action<double> seek)
        {
            // Clamped for this pass only: the layout pass hands a placeholder area, which
            // would shrink the height the user chose every time.
            var maxHeight = Mathf.Max(kMinHeight, area.height - kMinVideoHeight - kSplitterHeight);
            var height = Mathf.Clamp(m_Height, kMinHeight, maxHeight);
            var splitter = new Rect(area.x, area.yMax - height - kSplitterHeight, area.width, kSplitterHeight);
            DoSplitter(splitter, area.yMax, maxHeight);
            height = Mathf.Clamp(m_Height, kMinHeight, maxHeight);

            var panel = new Rect(area.x, area.yMax - height, area.width, height);
            var above = new Rect(area.x, area.y, area.width, Mathf.Max(0, panel.y - kSplitterHeight - area.y));

            // Within half a frame of the shown one: frame times are rounded to the frame rate.
            var tolerance = frameDuration * 0.5;
            var first = FirstAtOrAfter(time - tolerance);
            var end = FirstAtOrAfter(time + tolerance);

            var title = new Rect(panel.x, panel.y, panel.width, EditorGUIUtility.singleLineHeight);
            GUI.Label(title, Styles.Title, EditorStyles.miniBoldLabel);
            var count = new GUIContent($"{end - first} at this frame, {m_Entries.Length} in all");
            var countWidth = EditorStyles.miniLabel.CalcSize(count).x;
            GUI.Label(new Rect(title.xMax - countWidth - kTextMargin, title.y, countWidth, title.height), count, EditorStyles.miniLabel);

            ApplySettings();
            var header = new Rect(panel.x, title.yMax, panel.width, AndroidLogcatStyles.columnHeader.CalcHeight(GUIContent.none, panel.width));
            DoHeaderGUI(header);

            var list = new Rect(panel.x, header.yMax, panel.width, Mathf.Max(0, panel.yMax - header.yMax));
            FollowFrame(list, time, first, end);
            DoRowsGUI(list, first, end, seek);
            return above;
        }

        /// <summary>
        /// Drags by its own flag rather than a control ID, which shifts between events
        /// when anything drawn before it asks for IDs on some events only.
        /// </summary>
        void DoSplitter(Rect rect, float bottom, float maxHeight)
        {
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);
            var id = GUIUtility.GetControlID(FocusType.Passive);
            var e = Event.current;
            switch (e.rawType)
            {
                case EventType.MouseDown:
                    if (e.button == 0 && rect.Contains(e.mousePosition))
                    {
                        m_Dragging = true;
                        // Keeps the mouse while it is outside the window.
                        GUIUtility.hotControl = id;
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (m_Dragging)
                    {
                        // From where the mouse is, so it stays on the divider.
                        m_Height = Mathf.Clamp(bottom - e.mousePosition.y - kSplitterHeight * 0.5f, kMinHeight, maxHeight);
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (m_Dragging)
                    {
                        m_Dragging = false;
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;
                case EventType.Repaint:
                    EditorGUI.DrawRect(new Rect(rect.x, rect.center.y, rect.width, 1), new Color(0, 0, 0, 0.3f));
                    break;
            }
        }

        /// <summary>
        /// When the frame changes, scrolls the shown frame's messages into view, or the
        /// last one logged before it when it has none.
        /// </summary>
        void FollowFrame(Rect list, double time, int first, int end)
        {
            if (time.Equals(m_ScrolledFor))
                return;
            m_ScrolledFor = time;
            var last = end - 1;
            if (last < 0)
            {
                m_Scroll.y = 0;
                return;
            }
            var top = Mathf.Max(0, first < end ? first : last) * m_RowHeight;
            var bottom = (last + 1) * m_RowHeight;
            if (bottom - m_Scroll.y > list.height)
                m_Scroll.y = bottom - list.height;
            if (top < m_Scroll.y)
                m_Scroll.y = top;
        }

        void ApplySettings()
        {
            var settings = m_Runtime.Settings;
            m_RowHeight = settings.MessageFontSize + 5;
            foreach (Priority priority in Enum.GetValues(typeof(Priority)))
            {
                var style = m_PriorityStyles[(int)priority] ??= new GUIStyle(AndroidLogcatStyles.priorityDefaultStyle);
                style.font = settings.MessageFont;
                style.fontSize = settings.MessageFontSize;
                style.fixedHeight = 0;
                style.normal.textColor = settings.GetMessageColor(priority);
            }
        }

        bool Shows(Column column)
        {
            if (column == Column.Video)
                return true;
            var data = m_Runtime.Settings.ColumnData[(int)ConsoleColumn(column)];
            // The Logcat window hides its icons when the font is too small for them.
            if (column == Column.Icon)
                return m_Runtime.Settings.MessageFontSize > 11 && data.enabled;
            return data.enabled;
        }

        static AndroidLogcatConsoleWindow.Column ConsoleColumn(Column column)
        {
            switch (column)
            {
                case Column.Icon: return AndroidLogcatConsoleWindow.Column.Icon;
                case Column.Time: return AndroidLogcatConsoleWindow.Column.Time;
                case Column.ProcessId: return AndroidLogcatConsoleWindow.Column.ProcessId;
                case Column.ThreadId: return AndroidLogcatConsoleWindow.Column.ThreadId;
                case Column.Priority: return AndroidLogcatConsoleWindow.Column.Priority;
                case Column.Tag: return AndroidLogcatConsoleWindow.Column.Tag;
                default: return AndroidLogcatConsoleWindow.Column.Message;
            }
        }

        static GUIContent HeaderFor(Column column)
        {
            switch (column)
            {
                case Column.Video: return Styles.Video;
                case Column.Icon: return GUIContent.none;
                case Column.ProcessId: return new GUIContent("Pid");
                case Column.ThreadId: return new GUIContent("Tid");
                default: return new GUIContent(column.ToString());
            }
        }

        /// <summary>The header, which also places the columns for the rows under it. The message takes what is left.</summary>
        void DoHeaderGUI(Rect header)
        {
            var x = 0f;
            foreach (Column column in Enum.GetValues(typeof(Column)))
            {
                m_ColumnX[(int)column] = x;
                if (!Shows(column))
                {
                    m_ColumnWidth[(int)column] = 0;
                    continue;
                }
                var width = column == Column.Video ? kVideoTimeWidth
                    : column == Column.Message ? Mathf.Max(0, header.width - x)
                    : m_Runtime.Settings.ColumnData[(int)ConsoleColumn(column)].width;
                m_ColumnWidth[(int)column] = width;
                GUI.Label(new Rect(header.x + x, header.y, width, header.height), HeaderFor(column), AndroidLogcatStyles.columnHeader);
                x += width;
            }
        }

        void DoRowsGUI(Rect list, int first, int end, Action<double> seek)
        {
            var content = new Rect(0, 0, list.width - kScrollbarWidth, m_Entries.Length * m_RowHeight);
            m_Scroll = GUI.BeginScrollView(list, m_Scroll, content, false, true);
            var from = Mathf.Max(0, Mathf.FloorToInt(m_Scroll.y / m_RowHeight));
            var to = Mathf.Min(m_Entries.Length, Mathf.CeilToInt((m_Scroll.y + list.height) / m_RowHeight) + 1);
            var e = Event.current;
            var color = GUI.color;
            for (var i = from; i < to; i++)
            {
                var row = new Rect(0, i * m_RowHeight, content.width, m_RowHeight);
                if (e.type == EventType.MouseDown && e.button == 0 && row.Contains(e.mousePosition))
                {
                    seek(m_FrameTimes[i]);
                    e.Use();
                }
                if (e.type != EventType.Repaint)
                    continue;
                // The Logcat window's stripes, and its selection for the shown frame.
                if (i >= first && i < end)
                    AndroidLogcatStyles.background.Draw(row, false, false, true, false);
                else
                    (i % 2 == 0 ? AndroidLogcatStyles.backgroundEven : AndroidLogcatStyles.backgroundOdd).Draw(row, false, false, false, false);
                GUI.color = i >= end ? new Color(color.r, color.g, color.b, color.a * kFutureAlpha) : color;
                DoRowGUI(row, m_Entries[i], m_FrameTimes[i]);
            }
            GUI.color = color;
            GUI.EndScrollView();
        }

        void DoRowGUI(Rect row, LogcatEntry entry, double frameTime)
        {
            var style = m_PriorityStyles[(int)entry.priority];
            if (Shows(Column.Icon))
            {
                var iconSize = AndroidLogcatStyles.kSmallIconSize;
                var icon = new Rect(m_ColumnX[(int)Column.Icon] + (m_ColumnWidth[(int)Column.Icon] - iconSize.x) * 0.5f,
                    row.y + (row.height - iconSize.y) * 0.5f, 0, 0);
                IconStyleFor(entry.priority).Draw(icon, GUIContent.none, 0);
            }
            DoCellGUI(row, Column.Video, AndroidLogcatVideoPlayer.FormatTime(frameTime, m_Minutes), style);
            DoCellGUI(row, Column.Time, entry.dateTime.ToLocalTime().ToString(LogcatEntry.s_TimeFormat), style);
            DoCellGUI(row, Column.ProcessId, entry.processId.ToString(), style);
            DoCellGUI(row, Column.ThreadId, entry.threadId.ToString(), style);
            DoCellGUI(row, Column.Priority, entry.priority.ToString(), style);
            DoCellGUI(row, Column.Tag, entry.tag, style);
            DoCellGUI(row, Column.Message, entry.message, style);
        }

        void DoCellGUI(Rect row, Column column, string value, GUIStyle style)
        {
            var width = m_ColumnWidth[(int)column];
            if (width <= kTextMargin)
                return;
            var cell = new Rect(m_ColumnX[(int)column] + kTextMargin, row.y, width - kTextMargin, row.height);
            style.Draw(cell, new GUIContent(value), 0);
        }

        static GUIStyle IconStyleFor(Priority priority)
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

        /// <summary>The first message belonging to a frame at or after <paramref name="time"/>.</summary>
        int FirstAtOrAfter(double time)
        {
            int low = 0, high = m_FrameTimes.Length;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                if (m_FrameTimes[middle] < time)
                    low = middle + 1;
                else
                    high = middle;
            }
            return low;
        }
    }
}
