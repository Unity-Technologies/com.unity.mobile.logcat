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
                "Show the device screen live. Streaming stops when another row is selected. " +
                "Right click to reconnect.");
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

        const string kRenameControlName = "ScreenshotRenameField";

        readonly Splitter m_Splitter = new Splitter(Splitter.SplitterType.Horizontal, kMinWidth, kMaxWidth);
        Vector2 m_Scroll;
        bool m_LiveSelected;

        // Which row is being renamed, and the text so far. The field is focused once,
        // the frame after it first appears.
        string m_RenamingPath;
        string m_RenameText;
        bool m_RenameNeedsFocus;
        // The list's own control id, remembered so that focus can go back to it once a
        // rename ends - otherwise the keys would need another click to work again.
        int m_ListControlId;

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
            m_ListControlId = controlId;

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
            // iterated here, and a menu has to be positioned in window coordinates rather
            // than the scroll view's.
            string deletePath = null;
            var deleteRow = -1;
            var menuRow = -1;
            string menuPath = null;
            var menuScreenPosition = Vector2.zero;

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

                if (row > 0 && screenshots[row - 1].Path == m_RenamingPath)
                {
                    DoRenameFieldGUI(labelRect);
                }
                else
                {
                    var label = row == 0
                        ? Styles.LiveRow
                        : new GUIContent(screenshots[row - 1].Name, screenshots[row - 1].Path);
                    var style = isSelected ? Styles.SelectedRow : EditorStyles.label;
                    GUI.Label(labelRect, label, style);
                }

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
                // delete button does not also change the selection. Skipped while this
                // row is being renamed, so clicking into the text field does not count
                // as selecting the row.
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                    && labelRect.Contains(Event.current.mousePosition)
                    && (row == 0 || screenshots[row - 1].Path != m_RenamingPath))
                {
                    GUIUtility.keyboardControl = controlId;
                    SelectRow(screenshots, row);

                    // The Live row has no file to open.
                    if (Event.current.clickCount == 2 && row > 0)
                        AndroidLogcatUtilities.OpenFile(screenshots[row - 1].Path);

                    Event.current.Use();
                }

                if (Event.current.type == EventType.ContextClick
                    && rowRect.Contains(Event.current.mousePosition))
                {
                    // Selected as well, so the menu acts on what is now on screen.
                    GUIUtility.keyboardControl = controlId;
                    SelectRow(screenshots, row);

                    menuRow = row;
                    menuPath = row == 0 ? null : screenshots[row - 1].Path;
                    // Captured in screen space: inside the scroll view the mouse position
                    // is in content coordinates, which the menu would misplace.
                    menuScreenPosition = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                    Event.current.Use();
                }
            }
            GUI.EndScrollView();

            HandleKeys(controlId, screenshots, rowCount, selectedRow, rowHeight, inner.height);

            if (menuRow == 0)
                ShowLiveRowContextMenu(GUIUtility.ScreenToGUIPoint(menuScreenPosition));
            else if (menuRow > 0)
                ShowRowContextMenu(menuPath, GUIUtility.ScreenToGUIPoint(menuScreenPosition));

            if (deletePath != null)
                ConfirmAndDelete(deletePath, deleteRow);
        }

        /// <summary>
        /// The Live row has no file behind it, so all it offers is starting the stream
        /// over - a server that died, or a device that went away and came back, otherwise
        /// needs the selection moved off the row and back onto it.
        /// </summary>
        void ShowLiveRowContextMenu(Vector2 position)
        {
            var menu = new AndroidContextMenu<ScreenshotContextMenu>();
            menu.Add(ScreenshotContextMenu.Reconnect, "Reconnect",
                enabled: m_SelectedDevice() != null);
            menu.Show(position, OnContextMenuSelection);
        }

        void ShowRowContextMenu(string path, Vector2 position)
        {
            var menu = new AndroidContextMenu<ScreenshotContextMenu>();
            menu.Add(ScreenshotContextMenu.ShowInFileBrowser,
                AndroidLogcatUtilities.RevealInFileBrowserLabel, userData: path);
            menu.Add(ScreenshotContextMenu.Open, "Open", userData: path);
            menu.Add(ScreenshotContextMenu.SaveAs, "Save As...", userData: path);
            menu.Add(ScreenshotContextMenu.Rename, "Rename", userData: path);
            menu.Show(position, OnContextMenuSelection);
        }

        /// <summary>
        /// The row's label replaced by a text field. Enter commits, Escape cancels, and
        /// losing focus commits as well - clicking away is not a reason to throw the name
        /// the user typed away.
        /// </summary>
        void DoRenameFieldGUI(Rect rc)
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    CommitRename();
                    e.Use();
                    return;
                }
                if (e.keyCode == KeyCode.Escape)
                {
                    CancelRename();
                    e.Use();
                    return;
                }
            }

            GUI.SetNextControlName(kRenameControlName);
            m_RenameText = EditorGUI.TextField(rc, m_RenameText);

            if (m_RenameNeedsFocus)
            {
                // Has to happen after the field exists, so a frame later than the menu.
                GUI.FocusControl(kRenameControlName);
                m_RenameNeedsFocus = false;
            }
            else if (GUI.GetNameOfFocusedControl() != kRenameControlName)
            {
                CommitRename();
            }
        }

        void BeginRename(string path)
        {
            m_RenamingPath = path;
            m_RenameText = Path.GetFileNameWithoutExtension(path);
            m_RenameNeedsFocus = true;
            m_Repaint();
        }

        void CommitRename()
        {
            var path = m_RenamingPath;
            var name = m_RenameText;
            CancelRename();

            if (path == null)
                return;

            // An unchanged or unusable name is not an error; the row just goes back to
            // showing what it showed before.
            m_CaptureScreenshot.RenameScreenshot(path, name);
            m_Repaint();
        }

        void CancelRename()
        {
            m_RenamingPath = null;
            m_RenameText = null;
            m_RenameNeedsFocus = false;
            if (GUI.GetNameOfFocusedControl() == kRenameControlName)
                GUIUtility.keyboardControl = m_ListControlId;
        }

        void OnContextMenuSelection(object userData, string[] options, int selected)
        {
            var menu = (AndroidContextMenu<ScreenshotContextMenu>)userData;
            var item = menu.GetItemAt(selected);
            if (item == null)
                return;

            var path = (string)item.UserData;
            switch (item.Item)
            {
                case ScreenshotContextMenu.ShowInFileBrowser:
                    AndroidLogcatUtilities.RevealInFileBrowser(path);
                    break;
                case ScreenshotContextMenu.Open:
                    AndroidLogcatUtilities.OpenFile(path);
                    break;
                case ScreenshotContextMenu.SaveAs:
                    SaveAs(path);
                    break;
                case ScreenshotContextMenu.Rename:
                    BeginRename(path);
                    break;
                case ScreenshotContextMenu.Reconnect:
                    // The context click selected the row, so the stream is this window's
                    // to restart by the time this runs.
                    RestartLiveStream();
                    m_Repaint();
                    break;
            }
        }

        void SaveAs(string path)
        {
            // Screenshots are always saved under the Screenshot mode's remembered
            // location, whatever mode the window happens to be in.
            var settings = m_Runtime.UserSettings.CaptureSettings;
            const AndroidLogcatScreenCaptureWindow.Mode mode = AndroidLogcatScreenCaptureWindow.Mode.Screenshot;

            var directory = AndroidLogcatUtilities.SaveFileAs(path, "Save Screenshot",
                settings.GetLastSaveLocation(mode));
            if (directory != null)
                settings.SetLastSaveLocation(mode, directory);
        }

        /// <summary>
        /// F2 everywhere, and Enter as well on macOS - the same bindings the Project
        /// window uses, so whichever one the user reaches for works.
        /// </summary>
        static bool IsRenameShortcut(Event e)
        {
            if (e.keyCode == KeyCode.F2)
                return true;
            return Application.platform == RuntimePlatform.OSXEditor
                && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
        }

        /// <summary>
        /// Delete everywhere, and Command+Backspace on macOS, where compact keyboards
        /// have no forward delete key - again what the Project window takes.
        /// </summary>
        static bool IsDeleteShortcut(Event e)
        {
            if (e.keyCode == KeyCode.Delete)
                return true;
            return Application.platform == RuntimePlatform.OSXEditor
                && e.keyCode == KeyCode.Backspace && e.command;
        }

        /// <summary>
        /// Up and Down cycle through the list once it has focus, F2 renames and Delete
        /// deletes.
        /// </summary>
        void HandleKeys(int controlId, IReadOnlyList<AndroidLogcatCaptureScreenshot.Screenshot> screenshots,
            int rowCount, int selectedRow, float rowHeight, float viewHeight)
        {
            // While the rename field has focus it owns the keyboard, so none of this runs.
            if (GUIUtility.keyboardControl != controlId || Event.current.type != EventType.KeyDown)
                return;

            if (IsRenameShortcut(Event.current))
            {
                // Row 0 is the live stream, which has no file to rename.
                if (selectedRow > 0)
                    BeginRename(screenshots[selectedRow - 1].Path);
                Event.current.Use();
                return;
            }

            if (IsDeleteShortcut(Event.current))
            {
                // Used before the dialog, which pumps its own events.
                Event.current.Use();
                // Row 0 is the live stream, which has no file to delete. Same
                // confirmation as the row's own button, and it runs from the same place
                // in the frame - after the scroll view has closed.
                if (selectedRow > 0)
                    ConfirmAndDelete(screenshots[selectedRow - 1].Path, selectedRow);
                return;
            }

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
