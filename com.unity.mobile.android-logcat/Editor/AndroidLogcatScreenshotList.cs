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

            internal static readonly GUIContent Device = new GUIContent("Device",
                "The device the screenshot was captured from, as its details file records it.");
            internal static readonly GUIContent OS = new GUIContent("OS",
                "The Android version the device was running.");
            internal static readonly GUIContent DisplaySize = new GUIContent("Display size",
                "The device's display resolution at the time, which is not the image size when the display was rotated or its size overridden.");
            internal static readonly GUIContent ImageSize = new GUIContent("Image size",
                "Size of the image in pixels, which is the resolution of the display it was captured from.");
            internal static readonly GUIContent FileSize = new GUIContent("File size",
                "Size of the file on disk.");
            internal static readonly GUIContent Captured = new GUIContent("Captured",
                "When the file was last written.");

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
        readonly Action m_Repaint;

        const string kRenameControlName = "ScreenshotRenameField";
        const string kUndefined = "Undefined";

        readonly Splitter m_Splitter = new Splitter(Splitter.SplitterType.Horizontal, kMinWidth, kMaxWidth);
        Vector2 m_Scroll;
        bool m_LiveSelected;
        // Whether the one-off "what should this window open on" decision has been made.
        bool m_InitialSelectionDone;

        // Which row is being renamed, and the text so far. The field is focused once,
        // the frame after it first appears.
        string m_RenamingPath;
        string m_RenameText;
        bool m_RenameNeedsFocus;
        // The list's own control id, remembered so that focus can go back to it once a
        // rename ends - otherwise the keys would need another click to work again.
        int m_ListControlId;

        // The selected screenshot, drawn by this window and nothing else. It is
        // deliberately not AndroidLogcatCaptureScreenshot's texture: that one is shared
        // with the Layout Viewer, which draws node bounds over it, and swapping it for
        // a screenshot picked out of this list would put that overlay on an unrelated
        // image. Only the selected path is shared.
        Texture2D m_PreviewTexture;
        string m_PreviewPath;
        long m_PreviewFileSize;
        DateTime m_PreviewWriteTime;
        AndroidLogcatScreenshotInfo m_PreviewInfo;

        // Zoom and pan for the preview. Its own, separate from the live view's: they
        // show different things, and a zoom set on one is rarely the one wanted on the
        // other. Kept across screenshots, though - screenshots from the same device are
        // the same size, so comparing two of them at the same zoom is the point.
        readonly AndroidLogcatImageViewer m_Viewer = new AndroidLogcatImageViewer();

        /// <summary>
        /// Whether the Live row is the selected one, so the caller knows to show the
        /// stream rather than an image, and that there is no file to open or save.
        /// </summary>
        internal bool LiveSelected => m_LiveSelected;

        internal AndroidLogcatScreenshotList(AndroidLogcatRuntimeBase runtime, Action repaint)
        {
            m_Runtime = runtime;
            m_CaptureScreenshot = runtime.CaptureScreenshot;
            m_LiveStream = runtime.LiveStream;
            m_Repaint = repaint;

            // Settings saved before the width existed deserialize it as 0, which would
            // collapse the list to nothing.
            var settings = m_Runtime.UserSettings.CaptureSettings;
            if (settings.ScreenshotListWidth < kMinWidth)
                settings.ScreenshotListWidth = kDefaultWidth;
        }

        /// <summary>
        /// Stops the stream and drops the selection, for a window that is going away or
        /// has switched to a mode that does not show this list. The initial selection is
        /// forgotten with it, so coming back decides what to open on again.
        /// </summary>
        internal void Deselect()
        {
            if (m_LiveSelected)
                m_LiveStream.StopStreaming();
            m_LiveSelected = false;
            m_InitialSelectionDone = false;
            DestroyPreview();
        }

        /// <summary>
        /// Forgets the loaded preview, so the next pass reads it from disk again even
        /// though the selected path has not changed. The file behind that path can have
        /// been replaced while the Editor was not looking.
        /// </summary>
        internal void InvalidatePreview()
        {
            DestroyPreview();
        }

        /// <summary>
        /// Draws the selected screenshot, or the last capture's error if there is one.
        /// Returns false when there is nothing to show, so the caller can say so.
        /// </summary>
        internal bool DoPreviewGUI(Rect rc)
        {
            var error = m_CaptureScreenshot.Error;
            if (!string.IsNullOrEmpty(error))
            {
                EditorGUI.HelpBox(rc, error, MessageType.Error);
                return true;
            }

            if (m_PreviewTexture == null)
                return false;

            // The same column the live view draws, so the two modes look alike. Taken
            // out of the area before the image is fitted, or the image would be drawn
            // underneath it.
            var statsWidth = AndroidLogcatStatsColumn.WidthFor(rc);
            var imageArea = new Rect(rc.x, rc.y, Mathf.Max(0, rc.width - statsWidth), rc.height);

            var imageBox = m_Viewer.DoGUI(imageArea,
                (float)m_PreviewTexture.width / m_PreviewTexture.height,
                imageRect => GUI.DrawTexture(imageRect, m_PreviewTexture), m_Repaint);

            DoStatsGUI(AndroidLogcatStatsColumn.RectBeside(rc, imageBox));
            return true;
        }

        void DoStatsGUI(Rect rc)
        {
            const float kLabelWidth = AndroidLogcatStatsColumn.kLabelWidth;
            var y = rc.y;

            AndroidLogcatStatsColumn.Row(rc, kLabelWidth, ref y, Styles.Device,
                m_PreviewInfo == null ? kUndefined : Value(m_PreviewInfo.deviceName),
                m_PreviewInfo?.deviceId);
            AndroidLogcatStatsColumn.Row(rc, kLabelWidth, ref y, Styles.OS,
                m_PreviewInfo == null ? kUndefined : OperatingSystem(m_PreviewInfo));
            AndroidLogcatStatsColumn.Row(rc, kLabelWidth, ref y, Styles.DisplaySize,
                m_PreviewInfo == null || m_PreviewInfo.displayWidth <= 0
                    ? kUndefined
                    : $"{m_PreviewInfo.displayWidth}x{m_PreviewInfo.displayHeight}");
            AndroidLogcatStatsColumn.Row(rc, kLabelWidth, ref y, Styles.ImageSize,
                $"{m_PreviewTexture.width}x{m_PreviewTexture.height}");
            AndroidLogcatStatsColumn.Row(rc, kLabelWidth, ref y, Styles.FileSize,
                EditorUtility.FormatBytes(m_PreviewFileSize));
            AndroidLogcatStatsColumn.Row(rc, kLabelWidth, ref y, Styles.Captured,
                m_PreviewWriteTime.ToString("g"), m_PreviewWriteTime.ToString("F"));
        }

        static string Value(string value)
        {
            return string.IsNullOrEmpty(value) ? kUndefined : value;
        }

        static string OperatingSystem(AndroidLogcatScreenshotInfo info)
        {
            if (string.IsNullOrEmpty(info.osVersion))
                return info.apiLevel > 0 ? $"API {info.apiLevel}" : kUndefined;
            return info.apiLevel > 0 ? $"Android {info.osVersion} (API {info.apiLevel})" : $"Android {info.osVersion}";
        }

        /// <summary>
        /// Loads whatever the selection points at, if it is not already loaded. Called
        /// every pass rather than from each place that can change the selection - a
        /// capture landing, a delete, a rename - so there is one path to get wrong
        /// instead of four.
        /// </summary>
        void SyncPreview()
        {
            var path = m_CaptureScreenshot.SelectedImagePath;
            if (path == m_PreviewPath)
                return;

            DestroyPreview();
            m_PreviewPath = path;

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            var texture = new Texture2D(2, 2);
            if (texture.LoadImage(File.ReadAllBytes(path)))
            {
                m_PreviewTexture = texture;
                var file = new FileInfo(path);
                m_PreviewFileSize = file.Length;
                m_PreviewWriteTime = file.LastWriteTime;
                m_PreviewInfo = AndroidLogcatScreenshotInfo.Load(path);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        void DestroyPreview()
        {
            if (m_PreviewTexture != null)
                UnityEngine.Object.DestroyImmediate(m_PreviewTexture);
            m_PreviewTexture = null;
            m_PreviewPath = null;
            m_PreviewFileSize = 0;
            m_PreviewWriteTime = default;
            m_PreviewInfo = null;
        }

        /// <summary>
        /// A stream belongs to the device it was started on, so it has to be restarted
        /// against a new one.
        /// </summary>
        internal void OnDeviceChanged(IAndroidLogcatDevice device)
        {
            if (m_LiveSelected)
                m_LiveStream.RestartStreaming(device);
        }

        /// <summary>
        /// Draws the list and the splitter, and returns what is left for the caller to
        /// draw the image or the stream into.
        /// </summary>
        internal Rect DoGUI(Rect rc, IAndroidLogcatDevice device)
        {
            var settings = m_Runtime.UserSettings.CaptureSettings;
            var width = settings.ScreenshotListWidth;

            var listRect = new Rect(rc.x, rc.y, width, rc.height);
            var splitterRect = new Rect(listRect.xMax, rc.y, kSplitterWidth, rc.height);

            DoListGUI(listRect, device);

            if (m_Splitter.DoGUI(splitterRect, ref width))
            {
                settings.ScreenshotListWidth = width;
                m_Repaint();
            }

            return new Rect(splitterRect.xMax, rc.y, Mathf.Max(0, rc.xMax - splitterRect.xMax), rc.height);
        }

        void DoListGUI(Rect rc, IAndroidLogcatDevice device)
        {
            // Allocated on every pass, before any early return, so control ids do not
            // shift between the Layout and Repaint passes.
            var controlId = GUIUtility.GetControlID(FocusType.Keyboard);
            m_ListControlId = controlId;

            GUI.Box(rc, GUIContent.none, EditorStyles.helpBox);

            // Every device, not just the selected one: a screenshot is worth looking at
            // whichever device it came from, and the file name says which that was.
            var screenshots = m_CaptureScreenshot.GetScreenshots();

            SyncPreview();

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

            // With no screenshots to look at, open on the live view rather than on an
            // empty pane. Once per window, and only when the list is empty: deleting the
            // last screenshot deliberately leaves nothing selected rather than starting a
            // stream, and that has to stay true.
            if (!m_InitialSelectionDone)
            {
                m_InitialSelectionDone = true;
                if (screenshots.Count == 0 && !m_LiveSelected)
                {
                    SelectRow(screenshots, 0, device);
                    selectedRow = 0;
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
                    // Tooltip relative to the project, because the absolute path is
                    // mostly project folder and covers the rows around it.
                    var label = row == 0
                        ? Styles.LiveRow
                        : new GUIContent(screenshots[row - 1].Name,
                            AndroidLogcatUtilities.ProjectRelativePath(screenshots[row - 1].Path));
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
                    SelectRow(screenshots, row, device);

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
                    SelectRow(screenshots, row, device);

                    menuRow = row;
                    menuPath = row == 0 ? null : screenshots[row - 1].Path;
                    // Captured in screen space: inside the scroll view the mouse position
                    // is in content coordinates, which the menu would misplace.
                    menuScreenPosition = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                    Event.current.Use();
                }
            }
            GUI.EndScrollView();

            HandleKeys(controlId, screenshots, rowCount, selectedRow, rowHeight, inner.height, device);

            if (menuRow == 0)
                ShowLiveRowContextMenu(GUIUtility.ScreenToGUIPoint(menuScreenPosition), device);
            else if (menuRow > 0)
                ShowRowContextMenu(menuPath, GUIUtility.ScreenToGUIPoint(menuScreenPosition));

            if (deletePath != null)
                ConfirmAndDelete(deletePath, deleteRow, device);
        }

        /// <summary>
        /// The Live row has no file behind it, so all it offers is starting the stream
        /// over - a server that died, or a device that went away and came back, otherwise
        /// needs the selection moved off the row and back onto it.
        /// </summary>
        void ShowLiveRowContextMenu(Vector2 position, IAndroidLogcatDevice device)
        {
            var menu = new AndroidContextMenu<ScreenshotContextMenu>();
            // The device travels in the menu item, because the menu is answered long
            // after this method has returned - the same reason the screenshot rows put
            // their path there. Named argument: the third positional parameter of Add
            // is `selected`, not `enabled`, and reconnecting without a device to
            // reconnect to does nothing.
            menu.Add(ScreenshotContextMenu.Reconnect, "Reconnect",
                enabled: device != null, userData: device);
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

            // What UserData holds depends on the item: a path for the screenshot rows,
            // the device for the Live row.
            switch (item.Item)
            {
                case ScreenshotContextMenu.ShowInFileBrowser:
                    AndroidLogcatUtilities.RevealInFileBrowser((string)item.UserData);
                    break;
                case ScreenshotContextMenu.Open:
                    AndroidLogcatUtilities.OpenFile((string)item.UserData);
                    break;
                case ScreenshotContextMenu.SaveAs:
                    SaveAs((string)item.UserData);
                    break;
                case ScreenshotContextMenu.Rename:
                    BeginRename((string)item.UserData);
                    break;
                case ScreenshotContextMenu.Reconnect:
                    // The context click selected the row, so the stream is this window's
                    // to restart by the time this runs.
                    m_LiveStream.RestartStreaming((IAndroidLogcatDevice)item.UserData);
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
            int rowCount, int selectedRow, float rowHeight, float viewHeight, IAndroidLogcatDevice device)
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
                    ConfirmAndDelete(screenshots[selectedRow - 1].Path, selectedRow, device);
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
                SelectRow(screenshots, next, device);
                ScrollIntoView(next, rowHeight, viewHeight);
            }
            Event.current.Use();
        }

        /// <summary>
        /// Row 0 shows the live stream, the rest a saved screenshot. Streaming starts and
        /// stops with the selection rather than needing its own button, so leaving the
        /// Live row does not leave the device mirroring for nothing.
        /// </summary>
        void SelectRow(IReadOnlyList<AndroidLogcatCaptureScreenshot.Screenshot> screenshots, int row,
            IAndroidLogcatDevice device)
        {
            if (row == 0)
            {
                if (!m_LiveSelected)
                {
                    m_LiveSelected = true;
                    m_LiveStream.RestartStreaming(device);
                }
            }
            else
            {
                if (m_LiveSelected)
                {
                    m_LiveSelected = false;
                    m_LiveStream.StopStreaming();
                }
                m_CaptureScreenshot.SelectImage(screenshots[row - 1].Path);
            }
            m_Repaint();
        }

        /// <summary>
        /// Deleting is confirmed first: the button sits next to the row one clicks to
        /// select it, and the file is gone for good afterwards. The deletion itself is
        /// AndroidLogcatCaptureScreenshot.DeleteScreenshot; what belongs here is the
        /// prompt, which the Layout Viewer sharing that instance should not inherit, and
        /// picking what to select next.
        /// </summary>
        void ConfirmAndDelete(string path, int row, IAndroidLogcatDevice device)
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
                    SelectRow(remaining, Mathf.Clamp(row - 1, 0, remaining.Count - 1) + 1, device);
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
