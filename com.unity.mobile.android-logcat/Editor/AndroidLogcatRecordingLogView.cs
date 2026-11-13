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
                "Each message starts with the video time of its frame, then the device's time. " +
                "Highlighted: logged since the previous frame. Click a message to go to its frame.");
            internal static readonly GUIStyle Row = new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip };
            internal static readonly Color Current = new Color(0.24f, 0.48f, 0.9f, 0.35f);
        }

        const float kRowHeight = 16;
        const float kSplitterHeight = 5;
        const float kMinHeight = 60;
        const float kMinVideoHeight = 100;
        const float kDefaultHeight = 160;
        // The time into the video, as the player shows it, then the device's clock.
        const float kVideoTimeWidth = 56;
        const float kTimeWidth = 84;
        const float kPriorityWidth = 14;
        const float kTagWidth = 150;
        const float kColumnGap = 6;
        const float kScrollbarWidth = 16;
        // Rows after the shown frame, which the video has not reached.
        const float kFutureAlpha = 0.45f;

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

            var header = new Rect(panel.x, panel.y, panel.width, EditorGUIUtility.singleLineHeight);
            GUI.Label(header, Styles.Title, EditorStyles.miniBoldLabel);
            var count = new GUIContent($"{end - first} at this frame, {m_Entries.Length} in all");
            var countWidth = EditorStyles.miniLabel.CalcSize(count).x;
            GUI.Label(new Rect(header.xMax - countWidth - kColumnGap, header.y, countWidth, header.height), count, EditorStyles.miniLabel);

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
            var top = Mathf.Max(0, first < end ? first : last) * kRowHeight;
            var bottom = (last + 1) * kRowHeight;
            if (bottom - m_Scroll.y > list.height)
                m_Scroll.y = bottom - list.height;
            if (top < m_Scroll.y)
                m_Scroll.y = top;
        }

        void DoRowsGUI(Rect list, int first, int end, Action<double> seek)
        {
            var content = new Rect(0, 0, list.width - kScrollbarWidth, m_Entries.Length * kRowHeight);
            m_Scroll = GUI.BeginScrollView(list, m_Scroll, content, false, true);
            var from = Mathf.Max(0, Mathf.FloorToInt(m_Scroll.y / kRowHeight));
            var to = Mathf.Min(m_Entries.Length, Mathf.CeilToInt((m_Scroll.y + list.height) / kRowHeight) + 1);
            var e = Event.current;
            var color = GUI.color;
            for (var i = from; i < to; i++)
            {
                var row = new Rect(0, i * kRowHeight, content.width, kRowHeight);
                if (e.type == EventType.MouseDown && e.button == 0 && row.Contains(e.mousePosition))
                {
                    seek(m_FrameTimes[i]);
                    e.Use();
                }
                if (e.type != EventType.Repaint)
                    continue;
                if (i >= first && i < end)
                    EditorGUI.DrawRect(row, Styles.Current);
                GUI.color = i >= end ? new Color(color.r, color.g, color.b, color.a * kFutureAlpha) : color;
                DoRowGUI(row, m_Entries[i], m_FrameTimes[i]);
            }
            GUI.color = color;
            GUI.EndScrollView();
        }

        void DoRowGUI(Rect row, LogcatEntry entry, double frameTime)
        {
            var style = Styles.Row;
            style.normal.textColor = m_Runtime.Settings.GetMessageColor(entry.priority);
            var x = row.x + kColumnGap;
            GUI.Label(new Rect(x, row.y, kVideoTimeWidth, row.height),
                AndroidLogcatVideoPlayer.FormatTime(frameTime, m_Minutes), style);
            x += kVideoTimeWidth;
            GUI.Label(new Rect(x, row.y, kTimeWidth, row.height), entry.dateTime.ToLocalTime().ToString("HH:mm:ss.fff"), style);
            x += kTimeWidth;
            GUI.Label(new Rect(x, row.y, kPriorityWidth, row.height), entry.priority.ToString().Substring(0, 1), style);
            x += kPriorityWidth + kColumnGap;
            GUI.Label(new Rect(x, row.y, kTagWidth, row.height), entry.tag, style);
            x += kTagWidth + kColumnGap;
            GUI.Label(new Rect(x, row.y, Mathf.Max(0, row.xMax - x), row.height), new GUIContent(entry.message, entry.message), style);
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
