using System;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// The captures taken from a device - screenshots, and recordings once there are
    /// any - with the Live button above them, and the splitter that separates the
    /// column from whatever is being shown on the right.
    /// <para>
    /// One per window rather than one per runtime: the scroll position, the splitter
    /// width and which row is selected are all view state, while
    /// <see cref="AndroidLogcatCaptureScreenshot"/> is runtime-wide. Files, numbering
    /// and the image itself stay there; this only decides what to look at.
    /// </para>
    /// </summary>
    internal class AndroidLogcatCaptureList
    {
        static class Styles
        {
            internal static readonly GUIContent Live = new GUIContent("Live",
                "Show the device screen live. Streaming stops when a capture is selected.");
            // The Editor's own asset icons, so a row reads as what it holds before it
            // is read. Through IconContent, which is what picks the icon for the skin.
            static readonly GUIContent kImage = EditorGUIUtility.IconContent("Image Icon");
            static readonly GUIContent kVideo = EditorGUIUtility.IconContent("VideoPlayer Icon");

            /// <summary>The icon for a capture, by what kind of file it is.</summary>
            internal static Texture IconFor(string path)
            {
                var extension = Path.GetExtension(path);
                var video = extension == ".mp4" || extension == ".webm";
                var icon = video ? kVideo : kImage;
                return icon != null ? icon.image : null;
            }

            internal static readonly GUIContent Captures = new GUIContent("Captures",
                "Everything captured from a device, from every device. Shift click and " +
                "Ctrl click select more than one; Ctrl+A selects all.");
            internal static readonly GUIContent Device = new GUIContent("Device",
                "The device the screenshot was captured from, as its details file records it.");
            internal static readonly GUIContent OS = new GUIContent("OS",
                "The Android version the device was running.");
            internal static readonly GUIContent DisplaySize = new GUIContent("Display Size",
                "The device's display resolution at the time, which is not the image size when the display was rotated or its size overridden.");
            internal static readonly GUIContent ImageSize = new GUIContent("Image Size",
                "Size of the image in pixels, which is the resolution of the display it was captured from.");
            internal static readonly GUIContent FileSize = new GUIContent("File Size",
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

        static readonly ProfilerMarker k_LoadPreview = new ProfilerMarker("AndroidLogcat.LoadPreview");

        internal const float kDefaultWidth = 220;
        const float kMinWidth = 150;
        const float kMaxWidth = 400;
        const float kSplitterWidth = 5;
        const float kMinPreviewWidth = 100;
        const float kScrollbarWidth = 16;
        const float kIconSize = 16;
        const float kIconMargin = 2;
        // Air between the Live button and the captures under it.
        const float kGroupGap = 5;
        // How long a new capture's row is lit, and how bright it starts.
        const double kFlashSeconds = 0.6;
        const float kFlashStrength = 0.6f;

        readonly AndroidLogcatRuntimeBase m_Runtime;
        readonly AndroidLogcatCaptureScreenshot m_CaptureScreenshot;
        readonly AndroidLogcatLiveStream m_LiveStream;
        readonly Action m_Repaint;

        const string kRenameControlName = "ScreenshotRenameField";
        const string kUndefined = "Undefined";

        readonly Splitter m_Splitter = new Splitter(Splitter.SplitterType.Horizontal, kMinWidth, kMaxWidth);
        Vector2 m_Scroll;
        bool m_LiveSelected;

        // The selection, by path: a rescan renumbers rows, where a path stays itself.
        // The one being previewed is the capture screenshot's own selection, which is
        // always one of these.
        readonly HashSet<string> m_Selected = new HashSet<string>();
        // Where a shift click measures from.
        string m_SelectionAnchor;
        // The device the column was last drawn with. A context menu is answered after
        // the frame that opened it, so it cannot be handed one then.
        IAndroidLogcatDevice m_SelectedDevice;
        // What the list held last pass, so a selection can be pruned of files that
        // have gone without walking it on every repaint.
        int m_KnownCount = -1;
        // Whether the one-off "what should this window open on" decision has been made.
        bool m_InitialSelectionDone;

        // The capture that has just landed, lit until m_FlashUntil. The window is
        // showing the device rather than the list when one arrives, so the row is what
        // says where it went.
        string m_FlashPath;
        double m_FlashUntil;
        bool m_FlashNeedsScroll;

        // Which row is being renamed, and the text so far. The field is focused once,
        // the frame after it first appears.
        string m_RenamingPath;
        string m_RenameText;
        bool m_RenameNeedsFocus;
        // The list's own control id, remembered so that focus can go back to it once a
        // rename ends - otherwise the keys would need another click to work again.
        int m_ListControlId;

        // The selected screenshot, drawn by this window and nothing else. It is
        // deliberately not AndroidLogcatCaptureScreenshot's texture: that one is the
        // last capture, and swapping it for a screenshot picked out of this list would
        // lose it. Only the selected path is shared.
        Texture2D m_PreviewTexture;
        string m_PreviewPath;
        PreviewDetails m_PreviewDetails;

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

        internal AndroidLogcatCaptureList(AndroidLogcatRuntimeBase runtime, Action repaint)
        {
            m_Runtime = runtime;
            m_CaptureScreenshot = runtime.CaptureScreenshot;
            m_LiveStream = runtime.LiveStream;
            m_Repaint = repaint;

            // Settings saved before the width existed deserialize it as 0, which would
            // collapse the list to nothing.
            var settings = m_Runtime.UserSettings.CaptureSettings;
            if (settings.CaptureListWidth < kMinWidth)
                settings.CaptureListWidth = kDefaultWidth;
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
            m_Selected.Clear();
            m_SelectionAnchor = null;
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
        /// Lights a row, for a capture that has just been taken. The selection is left
        /// alone: it was taken from the live view, which is worth staying on.
        /// </summary>
        internal void Flash(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            m_FlashPath = path;
            m_FlashUntil = EditorApplication.timeSinceStartup + kFlashSeconds;
            m_FlashNeedsScroll = true;
            m_Repaint();
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

            AndroidLogcatStatsColumn.DrawBox(rc);

            // The same column the live view draws, so the two modes look alike. Taken
            // out of the area before the image is fitted, or the image would be drawn
            // underneath it, and sized to its text, or a device name is cut in half.
            var statsWidth = AndroidLogcatStatsColumn.WidthFor(rc, m_PreviewDetails.Values);
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
            var details = m_PreviewDetails;

            AndroidLogcatStatsColumn.Row(rc, kLabelWidth, ref y, Styles.Device, details.Device, details.DeviceId);
            AndroidLogcatStatsColumn.Row(rc, kLabelWidth, ref y, Styles.OS, details.OS);
            AndroidLogcatStatsColumn.Row(rc, kLabelWidth, ref y, Styles.DisplaySize, details.DisplaySize);
            AndroidLogcatStatsColumn.Row(rc, kLabelWidth, ref y, Styles.ImageSize, details.ImageSize);
            AndroidLogcatStatsColumn.Row(rc, kLabelWidth, ref y, Styles.FileSize, details.FileSize);
            AndroidLogcatStatsColumn.Row(rc, kLabelWidth, ref y, Styles.Captured, details.Captured, details.CapturedInFull);
        }

        /// <summary>
        /// What the details column says about the selected screenshot, worked out when
        /// it is loaded: the column is measured against these before the image is
        /// fitted, and none of it changes while the same screenshot is shown.
        /// </summary>
        class PreviewDetails
        {
            internal string Device { get; }
            internal string DeviceId { get; }
            internal string OS { get; }
            internal string DisplaySize { get; }
            internal string ImageSize { get; }
            internal string FileSize { get; }
            internal string Captured { get; }
            internal string CapturedInFull { get; }
            internal string[] Values { get; }

            internal PreviewDetails(Texture2D texture, FileInfo file, AndroidLogcatScreenshotInfo info)
            {
                Device = info == null || string.IsNullOrEmpty(info.deviceName) ? kUndefined : info.deviceName;
                DeviceId = info?.deviceId;
                OS = info == null ? kUndefined : OperatingSystem(info);
                DisplaySize = info == null || info.displayWidth <= 0
                    ? kUndefined
                    : $"{info.displayWidth}x{info.displayHeight}";
                ImageSize = $"{texture.width}x{texture.height}";
                FileSize = EditorUtility.FormatBytes(file.Length);
                Captured = file.LastWriteTime.ToString("g");
                CapturedInFull = file.LastWriteTime.ToString("F");
                Values = new[] { Device, OS, DisplaySize, ImageSize, FileSize, Captured };
            }
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
            bool loaded;
            using (k_LoadPreview.Auto())
                loaded = texture.LoadImage(File.ReadAllBytes(path));

            if (loaded)
            {
                m_PreviewTexture = texture;
                m_PreviewDetails = new PreviewDetails(texture, new FileInfo(path),
                    AndroidLogcatScreenshotInfo.Load(path));
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
            m_PreviewDetails = null;
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
            var width = Mathf.Min(settings.CaptureListWidth,
                Mathf.Max(0, rc.width - kSplitterWidth - kMinPreviewWidth));

            // The fade runs on a clock, and a clock only moves here if something
            // repaints. Expired centrally rather than in the row, which is not drawn
            // at all while it is scrolled out of sight.
            if (m_FlashPath != null)
            {
                if (EditorApplication.timeSinceStartup >= m_FlashUntil)
                    m_FlashPath = null;
                else
                    m_Repaint();
            }

            var listRect = new Rect(rc.x, rc.y, width, rc.height);
            var splitterRect = new Rect(listRect.xMax, rc.y, kSplitterWidth, rc.height);

            DoColumnGUI(listRect, device);

            // Clamped to the window, so storing it unmoved would shrink a saved width
            // that this window is too narrow to show.
            var before = width;
            if (m_Splitter.DoGUI(splitterRect, ref width))
            {
                if (!Mathf.Approximately(width, before))
                    settings.CaptureListWidth = width;
                m_Repaint();
            }

            return new Rect(splitterRect.xMax, rc.y, Mathf.Max(0, rc.xMax - splitterRect.xMax), rc.height);
        }

        /// <summary>
        /// The Live button, and under it the captures. Live is a button rather than a
        /// row in the list: it is a view of the device rather than a file, it is not
        /// one of the things a selection can span, and it stays put while the list
        /// scrolls.
        /// </summary>
        void DoColumnGUI(Rect rc, IAndroidLogcatDevice device)
        {
            m_SelectedDevice = device;
            var rowHeight = EditorGUIUtility.singleLineHeight;

            var liveRect = new Rect(rc.x, rc.y, rc.width, rowHeight);
            DoLiveGUI(liveRect, device);

            var headerRect = new Rect(rc.x, liveRect.yMax + kGroupGap, rc.width, rowHeight);
            GUI.Label(headerRect, Styles.Captures, EditorStyles.miniBoldLabel);

            var listRect = new Rect(rc.x, headerRect.yMax, rc.width,
                Mathf.Max(0, rc.yMax - headerRect.yMax));
            DoCapturesGUI(listRect, device);
        }

        void DoLiveGUI(Rect rc, IAndroidLogcatDevice device)
        {
            // A toggle for the pressed look, but it only ever switches on here: what
            // switches it off is selecting a capture, the way one row of a list gives
            // way to another rather than being clicked off.
            EditorGUI.BeginChangeCheck();
            GUI.Toggle(rc, m_LiveSelected, Styles.Live, EditorStyles.miniButton);
            if (EditorGUI.EndChangeCheck())
                SetLive(true, device);
        }

        void DoCapturesGUI(Rect rc, IAndroidLogcatDevice device)
        {
            // Allocated on every pass, before any early return, so control ids do not
            // shift between the Layout and Repaint passes.
            var controlId = GUIUtility.GetControlID(FocusType.Keyboard);
            m_ListControlId = controlId;

            GUI.Box(rc, GUIContent.none, EditorStyles.helpBox);

            // Every device, not just the selected one: a capture is worth looking at
            // whichever device it came from, and the file name says which that was.
            var captures = m_CaptureScreenshot.GetScreenshots();

            SyncPreview();
            PruneSelection(captures);

            // With nothing selected - a first run, or a domain reload, which does not
            // remember what was selected - open on the live view rather than on an
            // empty pane. Once per window: deleting the last capture deliberately
            // leaves nothing selected rather than starting a stream.
            if (!m_InitialSelectionDone)
            {
                m_InitialSelectionDone = true;
                if (!m_LiveSelected && m_Selected.Count == 0)
                    SetLive(true, device);
            }

            var rowHeight = EditorGUIUtility.singleLineHeight;
            var inner = new Rect(rc.x + 1, rc.y + 1, rc.width - 2, rc.height - 2);

            // Room for the scrollbar is reserved only when there will be one. Reserving
            // it unconditionally leaves a dead strip that pushes the delete buttons away
            // from the right edge.
            var contentHeight = captures.Count * rowHeight;
            var scrollbarWidth = contentHeight > inner.height ? kScrollbarWidth : 0;
            var content = new Rect(0, 0, inner.width - scrollbarWidth, contentHeight);
            var hasFocus = GUIUtility.keyboardControl == controlId;

            // A row that cannot be seen cannot say anything.
            if (m_FlashNeedsScroll && m_FlashPath != null)
            {
                m_FlashNeedsScroll = false;
                var flashed = IndexOf(captures, m_FlashPath);
                if (flashed >= 0)
                    ScrollIntoView(flashed, rowHeight, inner.height);
            }

            // Acted on after the loop: a menu has to be positioned in window
            // coordinates rather than the scroll view's, and answering it can
            // invalidate the cached list that is being iterated here.
            string menuPath = null;
            var menuScreenPosition = Vector2.zero;

            m_Scroll = GUI.BeginScrollView(inner, m_Scroll, content);
            for (var row = 0; row < captures.Count; row++)
            {
                var path = captures[row].Path;
                var rowRect = new Rect(0, row * rowHeight, content.width, rowHeight);
                var isSelected = m_Selected.Contains(path);

                if (Event.current.type == EventType.Repaint && isSelected)
                {
                    // Dimmer when the list is not focused, the way editor lists behave.
                    EditorGUI.DrawRect(rowRect, hasFocus
                        ? new Color(0.24f, 0.48f, 0.90f, 0.85f)
                        : new Color(0.30f, 0.30f, 0.30f, 0.85f));
                }

                if (Event.current.type == EventType.Repaint && path == m_FlashPath)
                {
                    var left = (float)((m_FlashUntil - EditorApplication.timeSinceStartup) / kFlashSeconds);
                    EditorGUI.DrawRect(rowRect, new Color(1, 1, 1, Mathf.Clamp01(left) * kFlashStrength));
                }

                var iconRect = new Rect(rowRect.x + 4, rowRect.y + (rowRect.height - kIconSize) * 0.5f,
                    kIconSize, kIconSize);
                var labelRect = new Rect(iconRect.xMax + kIconMargin, rowRect.y,
                    Mathf.Max(0, rowRect.xMax - iconRect.xMax - kIconMargin), rowRect.height);

                var icon = Styles.IconFor(path);
                if (icon != null && Event.current.type == EventType.Repaint)
                    GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);

                if (path == m_RenamingPath)
                {
                    DoRenameFieldGUI(labelRect);
                }
                else
                {
                    // Tooltip relative to the project, because the absolute path is
                    // mostly project folder and covers the rows around it.
                    var label = new GUIContent(captures[row].Name,
                        AndroidLogcatUtilities.ProjectRelativePath(path));
                    var style = isSelected ? Styles.SelectedRow : EditorStyles.label;
                    GUI.Label(labelRect, label, style);
                }

                // The whole row, icon included. Skipped while this row is being
                // renamed, so clicking into the text field does not count as selecting
                // the row.
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                    && rowRect.Contains(Event.current.mousePosition) && path != m_RenamingPath)
                {
                    GUIUtility.keyboardControl = controlId;
                    ClickRow(captures, path, Event.current, device);

                    if (Event.current.clickCount == 2)
                        AndroidLogcatUtilities.OpenFile(path);

                    Event.current.Use();
                }

                if (Event.current.type == EventType.ContextClick
                    && rowRect.Contains(Event.current.mousePosition))
                {
                    GUIUtility.keyboardControl = controlId;
                    // A click inside the selection acts on the whole of it; one
                    // outside moves the selection there first, as everywhere else.
                    if (!m_Selected.Contains(path))
                        SelectOnly(path, device);

                    menuPath = path;
                    // Captured in screen space: inside the scroll view the mouse
                    // position is in content coordinates, which the menu would misplace.
                    menuScreenPosition = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                    Event.current.Use();
                }
            }

            GUI.EndScrollView();

            HandleKeys(controlId, captures, rowHeight, inner.height, device);

            if (menuPath != null)
                ShowRowContextMenu(captures, menuPath, GUIUtility.ScreenToGUIPoint(menuScreenPosition));
        }

        // ------------------------------------------------------------------
        // Selection
        // ------------------------------------------------------------------

        /// <summary>
        /// Drops paths whose files are gone, so that a selection cannot act on them.
        /// Only when the list has changed length: the common case is that it has not.
        /// </summary>
        void PruneSelection(IReadOnlyList<AndroidLogcatCaptureScreenshot.Screenshot> captures)
        {
            if (captures.Count == m_KnownCount)
                return;
            m_KnownCount = captures.Count;

            if (m_Selected.Count == 0)
                return;

            m_Selected.RemoveWhere(path => IndexOf(captures, path) < 0);
            if (m_SelectionAnchor != null && !m_Selected.Contains(m_SelectionAnchor))
                m_SelectionAnchor = null;
        }

        /// <summary>
        /// Points the selection at a renamed row: it is held by path, and the old one
        /// would match nothing.
        /// </summary>
        void FollowRename(string path, string renamed)
        {
            if (string.IsNullOrEmpty(renamed) || !m_Selected.Remove(path))
                return;

            m_Selected.Add(renamed);
            if (m_SelectionAnchor == path)
                m_SelectionAnchor = renamed;
        }

        static int IndexOf(IReadOnlyList<AndroidLogcatCaptureScreenshot.Screenshot> captures, string path)
        {
            for (var i = 0; i < captures.Count; i++)
            {
                if (captures[i].Path == path)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Watching the device and looking at a capture are the same piece of screen,
        /// so turning one on turns the other off.
        /// </summary>
        void SetLive(bool live, IAndroidLogcatDevice device)
        {
            if (m_LiveSelected == live)
                return;

            m_LiveSelected = live;
            if (live)
            {
                m_Selected.Clear();
                m_SelectionAnchor = null;
                m_CaptureScreenshot.SelectImage(null);
                m_LiveStream.RestartStreaming(device);
            }
            else
            {
                m_LiveStream.StopStreaming();
            }
            m_Repaint();
        }

        /// <summary>
        /// Shift extends from the last click, Ctrl - Cmd on macOS - adds and removes
        /// one, and a plain click replaces the selection, as lists elsewhere behave.
        /// </summary>
        void ClickRow(IReadOnlyList<AndroidLogcatCaptureScreenshot.Screenshot> captures, string path,
            Event e, IAndroidLogcatDevice device)
        {
            if (e.shift && m_SelectionAnchor != null)
                SelectRange(captures, m_SelectionAnchor, path, device);
            else if (EditorGUI.actionKey)
                ToggleSelected(path, device);
            else
                SelectOnly(path, device);
        }

        void SelectOnly(string path, IAndroidLogcatDevice device)
        {
            SetLive(false, device);
            m_Selected.Clear();
            m_Selected.Add(path);
            m_SelectionAnchor = path;
            m_CaptureScreenshot.SelectImage(path);
            m_Repaint();
        }

        void ToggleSelected(string path, IAndroidLogcatDevice device)
        {
            SetLive(false, device);

            if (!m_Selected.Remove(path))
            {
                m_Selected.Add(path);
                m_SelectionAnchor = path;
                // The one just added is the one to look at.
                m_CaptureScreenshot.SelectImage(path);
            }
            else if (m_CaptureScreenshot.SelectedImagePath == path)
            {
                // The previewed one was removed from the selection, so the preview
                // moves to whatever is still selected, or to nothing.
                m_CaptureScreenshot.SelectImage(m_Selected.Count > 0 ? First(m_Selected) : null);
            }

            m_Repaint();
        }

        void SelectRange(IReadOnlyList<AndroidLogcatCaptureScreenshot.Screenshot> captures,
            string fromPath, string toPath, IAndroidLogcatDevice device)
        {
            var from = IndexOf(captures, fromPath);
            var to = IndexOf(captures, toPath);
            if (from < 0 || to < 0)
            {
                SelectOnly(toPath, device);
                return;
            }

            SetLive(false, device);
            m_Selected.Clear();
            for (var i = Mathf.Min(from, to); i <= Mathf.Max(from, to); i++)
                m_Selected.Add(captures[i].Path);

            // The anchor stays where the range started, so dragging the other end
            // back and forth keeps measuring from the same row.
            m_SelectionAnchor = fromPath;
            m_CaptureScreenshot.SelectImage(toPath);
            m_Repaint();
        }

        void SelectAll(IReadOnlyList<AndroidLogcatCaptureScreenshot.Screenshot> captures,
            IAndroidLogcatDevice device)
        {
            if (captures.Count == 0)
                return;

            SetLive(false, device);
            m_Selected.Clear();
            foreach (var capture in captures)
                m_Selected.Add(capture.Path);

            m_SelectionAnchor = captures[0].Path;
            m_CaptureScreenshot.SelectImage(captures[captures.Count - 1].Path);
            m_Repaint();
        }

        static string First(HashSet<string> paths)
        {
            foreach (var path in paths)
                return path;
            return null;
        }

        /// <summary>The selection in the order it is shown, which is how it is deleted.</summary>
        List<string> SelectedInOrder(IReadOnlyList<AndroidLogcatCaptureScreenshot.Screenshot> captures)
        {
            var paths = new List<string>(m_Selected.Count);
            foreach (var capture in captures)
            {
                if (m_Selected.Contains(capture.Path))
                    paths.Add(capture.Path);
            }
            return paths;
        }

        /// <summary>
        /// What can be done with the row that was clicked, and with the selection it
        /// belongs to. The single item entries act on that row: opening, saving and
        /// renaming several at once means several dialogs, which is not what a menu
        /// click asks for.
        /// </summary>
        void ShowRowContextMenu(IReadOnlyList<AndroidLogcatCaptureScreenshot.Screenshot> captures,
            string path, Vector2 position)
        {
            var single = m_Selected.Count <= 1;
            var menu = new AndroidContextMenu<ScreenshotContextMenu>();

            menu.Add(ScreenshotContextMenu.ShowInFileBrowser,
                AndroidLogcatUtilities.RevealInFileBrowserLabel, enabled: single, userData: path);
            menu.Add(ScreenshotContextMenu.Open, "Open", enabled: single, userData: path);
            menu.Add(ScreenshotContextMenu.CopyTo,
                single ? "Copy To..." : $"Copy {m_Selected.Count} Captures To...");
            menu.Add(ScreenshotContextMenu.Rename, "Rename", enabled: single, userData: path);
            menu.Add(ScreenshotContextMenu.Delete,
                single ? "Delete" : $"Delete {m_Selected.Count} Captures");
            menu.Add(ScreenshotContextMenu.SelectAll, "Select All",
                enabled: m_Selected.Count < captures.Count);

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
            if (m_CaptureScreenshot.RenameScreenshot(path, name, out var renamed))
                FollowRename(path, renamed);
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
                case ScreenshotContextMenu.CopyTo:
                    // Read when the menu is answered, not when it was opened.
                    CopyTo(SelectedInOrder(m_CaptureScreenshot.GetScreenshots()));
                    break;
                case ScreenshotContextMenu.Rename:
                    BeginRename((string)item.UserData);
                    break;
                case ScreenshotContextMenu.Delete:
                    {
                        // Read again rather than carried in the menu item: the menu is
                        // answered long after it was opened.
                        var captures = m_CaptureScreenshot.GetScreenshots();
                        ConfirmAndDelete(captures, SelectedInOrder(captures), m_SelectedDevice);
                        break;
                    }
                case ScreenshotContextMenu.SelectAll:
                    SelectAll(m_CaptureScreenshot.GetScreenshots(), m_SelectedDevice);
                    break;
            }
        }

        /// <summary>
        /// Copies captures somewhere they will be kept. One is copied under a name of
        /// the user's choosing, several into a folder under the names they have - a
        /// dialog per file is not what one click asks for.
        /// </summary>
        void CopyTo(IReadOnlyList<string> paths)
        {
            if (paths.Count == 0)
                return;

            // Captures are always copied from the Screenshot mode's remembered
            // location, whatever mode the window happens to be in.
            var settings = m_Runtime.UserSettings.CaptureSettings;
            const AndroidLogcatScreenCaptureWindow.Mode mode =
                AndroidLogcatScreenCaptureWindow.Mode.Screenshot;

            if (paths.Count == 1)
            {
                settings.SaveFileAs(mode, paths[0], "Copy Screenshot");
                return;
            }

            var directory = EditorUtility.OpenFolderPanel($"Copy {paths.Count} Captures",
                settings.GetLastSaveLocation(mode), string.Empty);
            if (string.IsNullOrEmpty(directory))
                return;

            if (!ConfirmOverwrites(paths, directory))
                return;

            var copied = 0;
            foreach (var path in paths)
            {
                if (AndroidLogcatUtilities.CopyInto(path, directory))
                    copied++;
            }

            if (copied > 0)
                settings.SetLastSaveLocation(mode, Path.GetFullPath(directory));
        }

        /// <summary>
        /// Asks once about the files already in the folder, rather than once each.
        /// </summary>
        static bool ConfirmOverwrites(IReadOnlyList<string> paths, string directory)
        {
            var existing = 0;
            foreach (var path in paths)
            {
                if (File.Exists(Path.Combine(directory, Path.GetFileName(path))))
                    existing++;
            }

            if (existing == 0)
                return true;

            var what = existing == 1 ? "One capture" : $"{existing} captures";
            return EditorUtility.DisplayDialog("Copy Captures",
                $"{what} of the same name already exist in {directory}.\n\nReplace them?",
                "Replace", "Cancel");
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
        void HandleKeys(int controlId, IReadOnlyList<AndroidLogcatCaptureScreenshot.Screenshot> captures,
            float rowHeight, float viewHeight, IAndroidLogcatDevice device)
        {
            // While the rename field has focus it owns the keyboard, so none of this runs.
            if (GUIUtility.keyboardControl != controlId || Event.current.type != EventType.KeyDown)
                return;

            var e = Event.current;
            var selectedRow = IndexOf(captures, m_CaptureScreenshot.SelectedImagePath);

            if (e.keyCode == KeyCode.A && EditorGUI.actionKey)
            {
                SelectAll(captures, device);
                e.Use();
                return;
            }

            if (IsRenameShortcut(e))
            {
                // One name at a time: renaming is a text field on a row.
                if (m_Selected.Count == 1 && selectedRow >= 0)
                    BeginRename(captures[selectedRow].Path);
                e.Use();
                return;
            }

            if (IsDeleteShortcut(e))
            {
                // Used before the dialog, which pumps its own events.
                e.Use();
                // Same confirmation as a row's own button, and it runs from the same
                // place in the frame - after the scroll view has closed.
                ConfirmAndDelete(captures, SelectedInOrder(captures), device);
                return;
            }

            if (captures.Count == 0)
                return;

            var last = captures.Count - 1;
            int next;
            switch (e.keyCode)
            {
                // With nothing selected, Down starts at the top and Up at the bottom.
                case KeyCode.Home: next = 0; break;
                case KeyCode.End: next = last; break;
                case KeyCode.UpArrow: next = selectedRow < 0 ? last : selectedRow - 1; break;
                case KeyCode.DownArrow: next = selectedRow < 0 ? 0 : selectedRow + 1; break;
                default: return;
            }

            next = Mathf.Clamp(next, 0, last);

            if (next != selectedRow)
            {
                // Shift grows the selection the way shift clicking does, from wherever
                // the last plain click left the anchor.
                if (e.shift && m_SelectionAnchor != null)
                    SelectRange(captures, m_SelectionAnchor, captures[next].Path, device);
                else
                    SelectOnly(captures[next].Path, device);

                ScrollIntoView(next, rowHeight, viewHeight);
            }
            e.Use();
        }

        /// <summary>
        /// Deletes what was asked for, with one confirmation for the lot of it, and
        /// leaves the selection on whatever took the place of the first one deleted.
        /// </summary>
        void ConfirmAndDelete(IReadOnlyList<AndroidLogcatCaptureScreenshot.Screenshot> captures,
            IReadOnlyList<string> paths, IAndroidLogcatDevice device)
        {
            if (paths.Count == 0)
                return;

            var what = paths.Count == 1
                ? $"Delete {Path.GetFileNameWithoutExtension(paths[0])}?"
                : $"Delete {paths.Count} captures?";
            var files = paths.Count == 1 ? "The file is" : "The files are";
            if (!EditorUtility.DisplayDialog(paths.Count == 1 ? "Delete Capture" : "Delete Captures",
                $"{what}\n\n{files} removed from disk and this cannot be undone.",
                "Delete", "Cancel"))
                return;

            var firstRow = IndexOf(captures, paths[0]);
            foreach (var path in paths)
            {
                if (m_CaptureScreenshot.DeleteScreenshot(path))
                    m_Selected.Remove(path);
            }
            m_SelectionAnchor = null;

            // Whatever took the place of the first one, else the one before it.
            // Deliberately not Live, which would start streaming because a file was
            // deleted.
            var remaining = m_CaptureScreenshot.GetScreenshots();
            if (m_Selected.Count == 0 && remaining.Count > 0 && firstRow >= 0)
                SelectOnly(remaining[Mathf.Clamp(firstRow, 0, remaining.Count - 1)].Path, device);
            else if (remaining.Count == 0)
                m_CaptureScreenshot.SelectImage(null);

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
