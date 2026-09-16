using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// The list of saved screenshots, with the live stream as its first row, and the
    /// splitter that separates it from whatever is being shown on the right.
    /// <para>
    /// One per window rather than one per runtime: the scroll position, the splitter
    /// width and which row is selected are all view state, and
    /// <see cref="AndroidLogcatCaptureScreenshot"/> is a runtime-wide singleton shared
    /// with the Layout Viewer. Files, numbering and the image itself stay there; this
    /// only decides what to look at.
    /// </para>
    /// </summary>
    internal class AndroidLogcatScreenshotList
    {
        static class Styles
        {
            internal static readonly GUIContent LiveRow = new GUIContent("Live",
                "Show the device screen live. Streaming stops when another row is selected.");
            internal static readonly GUIContent Delete = new GUIContent("×",
                "Delete this screenshot from disk");

            // The selected row draws on a coloured background, where the default label
            // colour is hard to read.
            static GUIStyle s_SelectedRow;
            internal static GUIStyle SelectedRow
            {
                get
                {
                    if (s_SelectedRow == null)
                    {
                        s_SelectedRow = new GUIStyle(EditorStyles.label);
                        s_SelectedRow.normal.textColor = Color.white;
                    }
                    return s_SelectedRow;
                }
            }
        }

        internal const float kDefaultWidth = 220;
        const float kMinWidth = 150;
        const float kMaxWidth = 400;
        const float kSplitterWidth = 5;
        const float kScrollbarWidth = 16;
        const float kDeleteButtonWidth = 18;
        const float kDeleteButtonMargin = 2;

        readonly AndroidLogcatRuntimeBase m_Runtime;
        readonly AndroidLogcatCaptureScreenshot m_CaptureScreenshot;
        readonly AndroidLogcatLiveStream m_LiveStream;
        readonly Func<IAndroidLogcatDevice> m_SelectedDevice;
        readonly Action m_Repaint;

        readonly Splitter m_Splitter = new Splitter(Splitter.SplitterType.Horizontal, kMinWidth, kMaxWidth);
        Vector2 m_Scroll;
        bool m_LiveSelected;

        /// <summary>
        /// Whether the Live row is the selected one, so the caller knows to show the
        /// stream rather than an image, and that there is no file to open or save.
        /// </summary>
        internal bool LiveSelected => m_LiveSelected;

        internal AndroidLogcatScreenshotList(AndroidLogcatRuntimeBase runtime,
            Func<IAndroidLogcatDevice> selectedDevice, Action repaint)
        {
            m_Runtime = runtime;
            m_CaptureScreenshot = runtime.CaptureScreenshot;
            m_LiveStream = runtime.LiveStream;
            m_SelectedDevice = selectedDevice;
            m_Repaint = repaint;

            // Settings saved before the width existed deserialize it as 0, which would
            // collapse the list to nothing.
            var settings = m_Runtime.UserSettings.CaptureSettings;
            if (settings.ScreenshotListWidth < kMinWidth)
                settings.ScreenshotListWidth = kDefaultWidth;
        }

        /// <summary>Stops the stream, for a window that is going away.</summary>
        internal void Deselect()
        {
            if (m_LiveSelected)
                m_LiveStream.StopStreaming();
            m_LiveSelected = false;
        }

        /// <summary>
        /// A stream belongs to the device it was started on, so it has to be restarted
        /// against a new one.
        /// </summary>
        internal void OnDeviceChanged()
        {
            if (m_LiveSelected)
                RestartLiveStream();
        }

        /// <summary>
        /// Draws the list and the splitter, and returns what is left for the caller to
        /// draw the image or the stream into.
        /// </summary>
        internal Rect DoGUI(Rect rc)
        {
            var settings = m_Runtime.UserSettings.CaptureSettings;
            var width = settings.ScreenshotListWidth;

            var listRect = new Rect(rc.x, rc.y, width, rc.height);
            var splitterRect = new Rect(listRect.xMax, rc.y, kSplitterWidth, rc.height);

            DoListGUI(listRect);

            if (m_Splitter.DoGUI(splitterRect, ref width))
            {
                settings.ScreenshotListWidth = width;
                m_Repaint();
            }

            return new Rect(splitterRect.xMax, rc.y, Mathf.Max(0, rc.xMax - splitterRect.xMax), rc.height);
        }

        void DoListGUI(Rect rc)
        {
            // Allocated on every pass, before any early return, so control ids do not
            // shift between the Layout and Repaint passes.
            var controlId = GUIUtility.GetControlID(FocusType.Keyboard);

            GUI.Box(rc, GUIContent.none, EditorStyles.helpBox);

            // Every device, not just the selected one: a screenshot is worth looking at
            // whichever device it came from, and the file name says which that was.
            var screenshots = m_CaptureScreenshot.GetScreenshots();

            // Row 0 is the live stream, the rest are saved screenshots.
            var rowCount = screenshots.Count + 1;
            var selectedRow = m_LiveSelected ? 0 : -1;
            if (!m_LiveSelected)
            {
                var selectedPath = m_CaptureScreenshot.SelectedImagePath;
                for (var i = 0; i < screenshots.Count; i++)
                {
                    if (screenshots[i].Path == selectedPath)
                    {
                        selectedRow = i + 1;
                        break;
                    }
                }
            }

            var rowHeight = EditorGUIUtility.singleLineHeight;
            var inner = new Rect(rc.x + 1, rc.y + 1, rc.width - 2, rc.height - 2);

            // Room for the scrollbar is reserved only when there will be one. Reserving
            // it unconditionally leaves a dead strip that pushes the delete buttons away
            // from the right edge.
            var contentHeight = rowCount * rowHeight;
            var scrollbarWidth = contentHeight > inner.height ? kScrollbarWidth : 0;
            var content = new Rect(0, 0, inner.width - scrollbarWidth, contentHeight);
            var hasFocus = GUIUtility.keyboardControl == controlId;

            // Acted on after the loop: deleting invalidates the cached list that is being
            // iterated here.
            string deletePath = null;
            var deleteRow = -1;

            m_Scroll = GUI.BeginScrollView(inner, m_Scroll, content);
            for (var row = 0; row < rowCount; row++)
            {
                var rowRect = new Rect(0, row * rowHeight, content.width, rowHeight);
                var isSelected = row == selectedRow;

                if (Event.current.type == EventType.Repaint && isSelected)
                {
                    // Dimmer when the list is not focused, the way editor lists behave.
                    EditorGUI.DrawRect(rowRect, hasFocus
                        ? new Color(0.24f, 0.48f, 0.90f, 0.85f)
                        : new Color(0.30f, 0.30f, 0.30f, 0.85f));
                }

                // The Live row has no file behind it, so nothing to delete.
                var deleteWidth = row == 0 ? 0 : kDeleteButtonWidth + kDeleteButtonMargin * 2;
                var labelRect = new Rect(rowRect.x + 4, rowRect.y,
                    Mathf.Max(0, rowRect.width - 4 - deleteWidth), rowRect.height);

                var label = row == 0
                    ? Styles.LiveRow
                    : new GUIContent(screenshots[row - 1].Name, screenshots[row - 1].Path);
                var style = isSelected ? Styles.SelectedRow : EditorStyles.label;
                GUI.Label(labelRect, label, style);

                if (deleteWidth > 0)
                {
                    // Inset by a pixel top and bottom so the button does not touch the
                    // rows above and below it.
                    var deleteRect = new Rect(
                        rowRect.xMax - kDeleteButtonWidth - kDeleteButtonMargin,
                        rowRect.y + 1,
                        kDeleteButtonWidth,
                        rowRect.height - 2);
                    if (GUI.Button(deleteRect, Styles.Delete, EditorStyles.miniButton))
                    {
                        deletePath = screenshots[row - 1].Path;
                        deleteRow = row;
                    }
                }

                // Hit tested against the label rather than the whole row, so that the
                // delete button does not also change the selection.
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                    && labelRect.Contains(Event.current.mousePosition))
                {
                    GUIUtility.keyboardControl = controlId;
                    SelectRow(screenshots, row);
                    Event.current.Use();
                }
            }
            GUI.EndScrollView();

            HandleKeys(controlId, screenshots, rowCount, selectedRow, rowHeight, inner.height);

            if (deletePath != null)
                ConfirmAndDelete(deletePath, deleteRow);
        }

        /// <summary>Up and Down cycle through the list once it has focus.</summary>
        void HandleKeys(int controlId, IReadOnlyList<AndroidLogcatCaptureScreenshot.Screenshot> screenshots,
            int rowCount, int selectedRow, float rowHeight, float viewHeight)
        {
            if (GUIUtility.keyboardControl != controlId || Event.current.type != EventType.KeyDown)
                return;

            var delta = 0;
            switch (Event.current.keyCode)
            {
                case KeyCode.UpArrow: delta = -1; break;
                case KeyCode.DownArrow: delta = 1; break;
                case KeyCode.Home: delta = -rowCount; break;
                case KeyCode.End: delta = rowCount; break;
                default: return;
            }

            // No selection yet: Down starts at the top, Up at the bottom.
            var next = selectedRow < 0
                ? (delta > 0 ? 0 : rowCount - 1)
                : Mathf.Clamp(selectedRow + delta, 0, rowCount - 1);

            if (next != selectedRow)
            {
                SelectRow(screenshots, next);
                ScrollIntoView(next, rowHeight, viewHeight);
            }
            Event.current.Use();
        }

        /// <summary>
        /// Row 0 shows the live stream, the rest a saved screenshot. Streaming starts and
        /// stops with the selection rather than needing its own button, so leaving the
        /// Live row does not leave the device mirroring for nothing.
        /// </summary>
        void SelectRow(IReadOnlyList<AndroidLogcatCaptureScreenshot.Screenshot> screenshots, int row)
        {
            if (row == 0)
            {
                if (!m_LiveSelected)
                {
                    m_LiveSelected = true;
                    RestartLiveStream();
                }
            }
            else
            {
                if (m_LiveSelected)
                {
                    m_LiveSelected = false;
                    m_LiveStream.StopStreaming();
                }
                m_CaptureScreenshot.LoadImage(screenshots[row - 1].Path);
            }
            m_Repaint();
        }

        void RestartLiveStream()
        {
            m_LiveStream.StopStreaming();
            // The window keeps its own device selection, which is not necessarily what
            // the runtime-wide device query points at.
            var device = m_SelectedDevice();
            if (device != null)
                m_LiveStream.StartStreaming(device, OnLiveStreamCompleted);
        }

        void OnLiveStreamCompleted(AndroidLogcatLiveStream.Result result)
        {
            // Nothing to collect - a live stream leaves no file behind. On failure the
            // reason is in AndroidLogcatLiveStream.Errors, which its DoGUI shows.
            m_Repaint();
        }

        /// <summary>
        /// Deleting is confirmed first: the button sits next to the row one clicks to
        /// select it, and the file is gone for good afterwards. The deletion itself is
        /// AndroidLogcatCaptureScreenshot.DeleteScreenshot; what belongs here is the
        /// prompt, which the Layout Viewer sharing that instance should not inherit, and
        /// picking what to select next.
        /// </summary>
        void ConfirmAndDelete(string path, int row)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (!EditorUtility.DisplayDialog("Delete Screenshot",
                $"Delete {name}?\n\nThe file is removed from disk and this cannot be undone.",
                "Delete", "Cancel"))
                return;

            var wasSelected = m_CaptureScreenshot.SelectedImagePath == path;
            if (!m_CaptureScreenshot.DeleteScreenshot(path))
                return;

            if (wasSelected)
            {
                // Whatever took its place, else the one before it. Deliberately not the
                // Live row, which would start streaming because a file was deleted.
                var remaining = m_CaptureScreenshot.GetScreenshots();
                if (remaining.Count > 0)
                    SelectRow(remaining, Mathf.Clamp(row - 1, 0, remaining.Count - 1) + 1);
            }
            m_Repaint();
        }

        void ScrollIntoView(int index, float rowHeight, float viewHeight)
        {
            var top = index * rowHeight;
            if (top < m_Scroll.y)
                m_Scroll.y = top;
            else if (top + rowHeight > m_Scroll.y + viewHeight)
                m_Scroll.y = top + rowHeight - viewHeight;
        }
    }
}
