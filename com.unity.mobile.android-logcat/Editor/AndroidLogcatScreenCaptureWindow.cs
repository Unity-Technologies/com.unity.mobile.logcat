using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatScreenCaptureWindow : EditorWindow
    {
        class Styles
        {
            // Note: Info acquired from adb shell screenrecord --help
            public static GUIContent TimeLimit = new GUIContent("Time Limit", "Toggle to override time limit (in seconds), by default - time limit is 180 seconds.");
            public static GUIContent VideoSize = new GUIContent("Video Size", "Toggle to override video size, by default - device's main display resolution is used.");
            public static GUIContent BitRate = new GUIContent("Bit Rate", "Toggle to overide bit rate (in Kbps), the default is 2000Kbps.");
            public static GUIContent DisplayId = new GUIContent("Display Id", "Toggle to overide the display to record, the default is primary display, enter 'adb shell dumpsys SurfaceFlinger--display - id' in the terminal for valid display IDs. If empty string is provided primary display will be used.");
            public static GUIContent ShowInfo = new GUIContent("Show Info", "Display video information.");
            public static GUIContent Open = new GUIContent("Open", "Open captured screenshot or video.");
            public static GUIContent SaveAs = new GUIContent("Save As", "Save captured screenshot or video.");
            public static GUIContent CaptureScreenshot = new GUIContent("Capture", "Capture screenshot from the android device.");
            public static GUIContent CaptureVideo = new GUIContent("Capture", "Record the video from the android device, click Stop afterwards to stop the recording.");
            public static GUIContent StopVideo = new GUIContent("Stop", "Stop the recording.");
            public static GUIContent LiveStreamRow = new GUIContent("Live", "Show the device screen live. Streaming stops when another row is selected.");

            // The selected row draws on a coloured background, where the default label
            // colour is hard to read.
            private static GUIStyle s_SelectedScreenshotRow;
            public static GUIStyle SelectedScreenshotRow
            {
                get
                {
                    if (s_SelectedScreenshotRow == null)
                    {
                        s_SelectedScreenshotRow = new GUIStyle(EditorStyles.label);
                        s_SelectedScreenshotRow.normal.textColor = Color.white;
                    }
                    return s_SelectedScreenshotRow;
                }
            }
        }
        internal enum Mode
        {
            Screenshot,
            Video
        }
        private AndroidLogcatRuntimeBase m_Runtime;

        private const int kButtonAreaHeight = 30;
        private const int kBottomAreaHeight = 8;

        internal const float kDefaultScreenshotListWidth = 220;
        private const float kScreenshotListMinWidth = 150;
        private const float kScreenshotListMaxWidth = 400;
        private const float kSplitterWidth = 5;
        private AndroidLogcatCaptureScreenshot m_CaptureScreenshot;
        private AndroidLogcatCaptureVideo m_CaptureVideo;
        private AndroidLogcatVideoPlayer m_VideoPlayer;
        private AndroidLogcatLiveStream m_LiveStream;

        private AndroidLogcatDeviceSelection m_DeviceSelection;
        private IAndroidLogcatDevice m_LastDeviceUsedForAssets;

        private Splitter m_ScreenshotListSplitter;
        private Vector2 m_ScreenshotListScroll;

        /// <summary>
        /// Whether the "Live" row of the screenshot list is the selected one, in which
        /// case the preview shows the live stream instead of a saved image. Not
        /// persisted: reopening the window should not start streaming on its own.
        /// </summary>
        private bool m_LiveSelected;

        private bool IsCapturing
        {
            get
            {
                var mode = m_Runtime.UserSettings.CaptureSettings.Mode;
                switch (mode)
                {
                    case Mode.Screenshot: return m_CaptureScreenshot.IsCapturing || m_LiveStream.IsStreaming;
                    case Mode.Video: return m_CaptureVideo.IsRecording;
                    default:
                        throw new NotImplementedException(mode.ToString());
                }
            }
        }

        private string TemporaryPath
        {
            get
            {
                var mode = m_Runtime.UserSettings.CaptureSettings.Mode;
                switch (mode)
                {
                    // A live stream leaves no file behind, so there is nothing to open or
                    // save while its row is selected.
                    case Mode.Screenshot: return m_LiveSelected ? string.Empty : m_CaptureScreenshot.SelectedImagePath;
                    case Mode.Video: return m_CaptureVideo.GetVideoPath(m_DeviceSelection.SelectedDevice);
                    default:
                        throw new NotImplementedException(mode.ToString());
                }
            }
        }

        private string ExtensionForDialog
        {
            get
            {
                // Empty before the first capture, and while the Live row is selected. The
                // Save As button is disabled then, but this must not throw if it is ever
                // read outside that guard.
                var extension = Path.GetExtension(TemporaryPath);
                return string.IsNullOrEmpty(extension) ? string.Empty : extension.Substring(1);
            }
        }

        public static void ShowWindow()
        {
            GetWindow<AndroidLogcatScreenCaptureWindow>("Device Screen Capture");
        }

        private void OnEnable()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
                return;

            m_Runtime = AndroidLogcatManager.instance.Runtime;
            m_DeviceSelection = new AndroidLogcatDeviceSelection(m_Runtime, ReloadCaptureAssetsIfNeeded, nameof(AndroidLogcatScreenCaptureWindow) + "_DeviceId");
            m_Runtime.Closing += OnDisable;
            m_CaptureScreenshot = m_Runtime.CaptureScreenshot;
            m_CaptureVideo = m_Runtime.CaptureVideo;
            m_LiveStream = m_Runtime.LiveStream;
            m_VideoPlayer = new AndroidLogcatVideoPlayer();
            m_ScreenshotListSplitter = new Splitter(Splitter.SplitterType.Horizontal,
                kScreenshotListMinWidth, kScreenshotListMaxWidth);

            // Settings saved before this field existed deserialize it as 0, which would
            // collapse the list to nothing.
            var captureSettings = m_Runtime.UserSettings.CaptureSettings;
            if (captureSettings.ScreenshotListWidth < kScreenshotListMinWidth)
                captureSettings.ScreenshotListWidth = kDefaultScreenshotListWidth;

            // Settings saved while the removed LiveStream mode was selected still hold
            // its value, which is now out of range and would throw in the switches above.
            if (!Enum.IsDefined(typeof(Mode), captureSettings.Mode))
                captureSettings.Mode = Mode.Screenshot;

            m_Runtime.DeviceQuery.UpdateConnectedDevicesList(true);
        }
        private void ReloadCaptureAssetsIfNeeded(IAndroidLogcatDevice device)
        {
            if (m_LastDeviceUsedForAssets == device)
                return;

            m_LastDeviceUsedForAssets = device;

            m_VideoPlayer.Play(m_CaptureVideo.GetVideoPath(device));
            m_Runtime.CaptureScreenshot.LoadImage(m_Runtime.CaptureScreenshot.GetLatestImagePath(device));

            // A stream belongs to the device it was started on, so it has to be restarted
            // against the new one.
            if (m_LiveSelected)
                RestartLiveStream();
        }

        private void OnDisable()
        {
            // The live stream is owned by the runtime, so it would otherwise keep
            // mirroring the device after the window that was showing it is gone.
            if (m_LiveStream != null)
                m_LiveStream.StopStreaming();
            m_LiveSelected = false;

            if (m_VideoPlayer != null)
            {
                m_VideoPlayer.Dispose();
                m_VideoPlayer = null;
            }
            if (!AndroidBridge.AndroidExtensionsInstalled)
                return;

            if (m_Runtime == null)
                return;
            m_DeviceSelection.Dispose();
            m_DeviceSelection = null;
            m_Runtime = null;
        }

        private void QueueScreenCapture()
        {
            m_CaptureScreenshot.QueueScreenCapture(m_DeviceSelection.SelectedDevice, OnScreenshotCompleted);
        }

        void OnScreenshotCompleted()
        {
            var texture = m_CaptureScreenshot.ImageTexture;
            if (texture != null)
                maxSize = new Vector2(Math.Max(texture.width, position.width), texture.height + kButtonAreaHeight);
            Repaint();
        }

        void OnVideoCompleted(AndroidLogcatCaptureVideo.Result result, string videoPath)
        {
            if (result == AndroidLogcatCaptureVideo.Result.Success)
                m_VideoPlayer.Play(videoPath);
        }

        void OnLiveStreamCompleted(AndroidLogcatLiveStream.Result result)
        {
            // Nothing to collect - a live stream leaves no file behind. On failure the
            // reason is in AndroidLogcatLiveStream.Errors, which DoGUI shows.
            Repaint();
        }

        void DoModeGUI()
        {
            m_Runtime.UserSettings.CaptureSettings.Mode = (Mode)EditorGUILayout.EnumPopup(m_Runtime.UserSettings.CaptureSettings.Mode, AndroidLogcatStyles.toolbarPopup);
        }

        void OnGUI()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
            {
                AndroidLogcatUtilities.ShowAndroidIsNotInstalledMessage();
                return;
            }

            EditorGUILayout.BeginVertical();
            GUILayout.Space(5);

            DoToolbarGUI();

            GUILayout.Space(10);
            if (m_DeviceSelection.SelectedDevice == null)
                EditorGUILayout.HelpBox("No valid device selected.", MessageType.Info);
            else
                DoPreviewGUI();

            EditorGUILayout.EndVertical();
        }

        private void DoToolbarGUI()
        {
            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);

            DoProgressGUI();
            m_DeviceSelection.DoGUI();

            DoModeGUI();
            DoCaptureGUI();
            DoOpenGUI();
            DoSaveAsGUI();

            EditorGUILayout.EndHorizontal();
        }

        private void DoProgressGUI()
        {
            AndroidLogcatUtilities.DrawProgressIcon(IsCapturing);
            if (IsCapturing)
                Repaint();
        }

        private void DoCaptureGUI()
        {
            EditorGUI.BeginDisabledGroup(m_DeviceSelection.SelectedDevice == null);
            switch (m_Runtime.UserSettings.CaptureSettings.Mode)
            {
                case Mode.Screenshot:
                    EditorGUI.BeginDisabledGroup(m_CaptureScreenshot.IsCapturing);
                    if (GUILayout.Button(Styles.CaptureScreenshot, AndroidLogcatStyles.toolbarButton))
                        QueueScreenCapture();
                    EditorGUI.EndDisabledGroup();
                    break;
                case Mode.Video:
                    if (m_CaptureVideo.IsRecording)
                    {
                        if (GUILayout.Button(Styles.StopVideo, AndroidLogcatStyles.toolbarButton))
                        {
                            m_CaptureVideo.StopRecording();
                        }
                    }
                    else
                    {
                        if (GUILayout.Button(Styles.CaptureVideo, AndroidLogcatStyles.toolbarButton))
                        {
                            TimeSpan? timeLimit = null;
                            uint? videoSizeX = null;
                            uint? videoSizeY = null;
                            ulong? bitRate = null;
                            string displayId = null;
                            var vs = m_Runtime.UserSettings.CaptureVideoSettings;

                            if (vs.TimeLimitEnabled)
                            {
                                timeLimit = TimeSpan.FromSeconds(vs.TimeLimit);
                            }
                            if (vs.VideoSizeEnabled)
                            {
                                videoSizeX = vs.VideoSizeX;
                                videoSizeY = vs.VideoSizeY;
                            }

                            if (vs.BitRateEnabled)
                                bitRate = vs.BitRateK * 1000;
                            if (vs.DisplayIdEnabled && !string.IsNullOrEmpty(vs.DisplayId))
                                displayId = vs.DisplayId;

                            m_CaptureVideo.StartRecording(m_DeviceSelection.SelectedDevice, OnVideoCompleted, timeLimit, videoSizeX, videoSizeY, bitRate, displayId);
                        }
                    }
                    break;
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DoOpenGUI()
        {
            EditorGUI.BeginDisabledGroup(!File.Exists(TemporaryPath));
            if (GUILayout.Button(Styles.Open, AndroidLogcatStyles.toolbarButton))
            {
                switch (Application.platform)
                {
                    case RuntimePlatform.OSXEditor:
                        System.Diagnostics.Process.Start("open", TemporaryPath);
                        break;
                    default:
                        Application.OpenURL(TemporaryPath);
                        break;
                }
            }

            EditorGUI.EndDisabledGroup();
        }

        private void DoSaveAsGUI()
        {
            EditorGUI.BeginDisabledGroup(!File.Exists(TemporaryPath));
            if (GUILayout.Button(Styles.SaveAs, AndroidLogcatStyles.toolbarButton))
            {
                var mode = m_Runtime.UserSettings.CaptureSettings.Mode;
                var path = EditorUtility.SaveFilePanel(
                    "Save Screen Capture",
                    m_Runtime.UserSettings.CaptureSettings.GetLastSaveLocation(mode),
                    Path.GetFileName(TemporaryPath),
                    ExtensionForDialog);
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        m_Runtime.UserSettings.CaptureSettings.SetLastSaveLocation(mode, Path.GetFullPath(Path.GetDirectoryName(path)));
                        File.Copy(TemporaryPath, path, true);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogErrorFormat("Failed to save to '{0}' as '{1}'.", path, ex.Message);
                    }
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        /// <summary>
        /// The list of saved screenshots on the left, the selected one on the right, a
        /// draggable splitter between them.
        /// </summary>
        private void DoScreenshotGUI(Rect rc)
        {
            var settings = m_Runtime.UserSettings.CaptureSettings;
            var listWidth = settings.ScreenshotListWidth;

            var listRect = new Rect(rc.x, rc.y, listWidth, rc.height);
            var splitterRect = new Rect(listRect.xMax, rc.y, kSplitterWidth, rc.height);
            var imageRect = new Rect(splitterRect.xMax, rc.y, Mathf.Max(0, rc.width - splitterRect.xMax), rc.height);

            DoScreenshotListGUI(listRect);

            if (m_ScreenshotListSplitter.DoGUI(splitterRect, ref listWidth))
            {
                settings.ScreenshotListWidth = listWidth;
                Repaint();
            }

            if (m_LiveSelected)
            {
                // The developer-mode details are drawn by DoGUI, in the info column.
                m_LiveStream.DoGUI(imageRect);
                // Frames arrive on the runtime's update, not on GUI events, so the window
                // has to keep repainting to show them.
                if (m_LiveStream.IsStreaming)
                    Repaint();
            }
            else if (!m_CaptureScreenshot.DoGUI(imageRect))
            {
                var message = m_DeviceSelection.SelectedDevice == null
                    ? "No screenshot to show."
                    : "No screenshot to show, click Capture button.";
                EditorGUI.HelpBox(imageRect, message, MessageType.Info);
            }
        }

        private void DoScreenshotListGUI(Rect rc)
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
            var content = new Rect(0, 0, inner.width - 16, rowCount * rowHeight);
            var hasFocus = GUIUtility.keyboardControl == controlId;

            m_ScreenshotListScroll = GUI.BeginScrollView(inner, m_ScreenshotListScroll, content);
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

                var label = row == 0
                    ? Styles.LiveStreamRow
                    : new GUIContent(screenshots[row - 1].Name, screenshots[row - 1].Path);
                var style = isSelected ? Styles.SelectedScreenshotRow : EditorStyles.label;
                GUI.Label(new Rect(rowRect.x + 4, rowRect.y, rowRect.width - 4, rowRect.height), label, style);

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                    && rowRect.Contains(Event.current.mousePosition))
                {
                    GUIUtility.keyboardControl = controlId;
                    SelectRow(screenshots, row);
                    Event.current.Use();
                }
            }
            GUI.EndScrollView();

            HandleScreenshotListKeys(controlId, screenshots, rowCount, selectedRow, rowHeight, inner.height);
        }

        /// <summary>Up and Down cycle through the list once it has focus.</summary>
        private void HandleScreenshotListKeys(int controlId, IReadOnlyList<AndroidLogcatCaptureScreenshot.Screenshot> screenshots,
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
                ScrollScreenshotIntoView(next, rowHeight, viewHeight);
            }
            Event.current.Use();
        }

        /// <summary>
        /// Row 0 shows the live stream, the rest a saved screenshot. Streaming starts and
        /// stops with the selection rather than needing its own button, so leaving the
        /// Live row does not leave the device mirroring for nothing.
        /// </summary>
        private void SelectRow(IReadOnlyList<AndroidLogcatCaptureScreenshot.Screenshot> screenshots, int row)
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
            Repaint();
        }

        private void RestartLiveStream()
        {
            m_LiveStream.StopStreaming();
            if (m_DeviceSelection.SelectedDevice != null)
                m_LiveStream.StartStreaming(m_DeviceSelection.SelectedDevice, OnLiveStreamCompleted);
        }

        private void ScrollScreenshotIntoView(int index, float rowHeight, float viewHeight)
        {
            var top = index * rowHeight;
            if (top < m_ScreenshotListScroll.y)
                m_ScreenshotListScroll.y = top;
            else if (top + rowHeight > m_ScreenshotListScroll.y + viewHeight)
                m_ScreenshotListScroll.y = top + rowHeight - viewHeight;
        }

        private void DoPreviewGUI()
        {
            switch (m_Runtime.UserSettings.CaptureSettings.Mode)
            {
                case Mode.Screenshot:
                    {
                        var rc = new Rect(0, kButtonAreaHeight * 2, position.width, position.height - kButtonAreaHeight - kBottomAreaHeight);
                        DoScreenshotGUI(rc);
                    }
                    break;
                case Mode.Video:
                    if (Unsupported.IsDeveloperMode())
                        m_CaptureVideo.DoDebuggingGUI();
                    DoVideoSettingsGUI();
                    GUILayout.Space(5);
                    if (IsCapturing)
                    {
                        EditorGUILayout.HelpBox($"Recording{new String('.', (int)(Time.realtimeSinceStartup * 3) % 4 + 1)}\nClick Stop to stop the recording.", MessageType.Info);
                        break;
                    }
                    if (m_CaptureVideo.Errors.Length > 0)
                    {
                        DoVideoErrorsGUI();
                    }
                    else
                    {
                        m_VideoPlayer.DoGUI(position);
                        if (m_VideoPlayer.IsPlaying())
                            Repaint();
                    }
                    break;
                default:
                    break;
            }
        }

        void DoVideoSettingsGUI()
        {
            var rs = m_Runtime.UserSettings.CaptureVideoSettings;
            var width = 100;
            EditorGUILayout.LabelField("Toggle to override recorder settings", EditorStyles.boldLabel);

            // Time Limit
            EditorGUILayout.BeginHorizontal();
            rs.TimeLimitEnabled = GUILayout.Toggle(rs.TimeLimitEnabled, Styles.TimeLimit, AndroidLogcatStyles.toolbarButton, GUILayout.Width(width));
            EditorGUI.BeginDisabledGroup(!rs.TimeLimitEnabled);
            rs.TimeLimit = (uint)EditorGUILayout.IntSlider((int)rs.TimeLimit, 1, 180);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            // Video Size
            EditorGUILayout.BeginHorizontal();
            rs.VideoSizeEnabled = GUILayout.Toggle(rs.VideoSizeEnabled, Styles.VideoSize, AndroidLogcatStyles.toolbarButton, GUILayout.Width(width));
            EditorGUI.BeginDisabledGroup(!rs.VideoSizeEnabled);
            rs.VideoSizeX = (uint)EditorGUILayout.IntSlider((int)rs.VideoSizeX, 100, 7680);
            rs.VideoSizeY = (uint)EditorGUILayout.IntSlider((int)rs.VideoSizeY, 100, 7680);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            // Bit Rate
            EditorGUILayout.BeginHorizontal();
            rs.BitRateEnabled = GUILayout.Toggle(rs.BitRateEnabled, Styles.BitRate, AndroidLogcatStyles.toolbarButton, GUILayout.Width(width));
            EditorGUI.BeginDisabledGroup(!rs.BitRateEnabled);
            rs.BitRateK = Math.Max(1, (uint)EditorGUILayout.IntField(GUIContent.none, (int)rs.BitRateK));
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            // Display Id
            EditorGUILayout.BeginHorizontal();
            rs.DisplayIdEnabled = GUILayout.Toggle(rs.DisplayIdEnabled, Styles.DisplayId, AndroidLogcatStyles.toolbarButton, GUILayout.Width(width));
            EditorGUI.BeginDisabledGroup(!rs.DisplayIdEnabled);
            Color? oldColor = null;
            if (rs.DisplayIdEnabled && string.IsNullOrEmpty(rs.DisplayId))
            {
                oldColor = GUI.color;
                GUI.color = Color.red;
            }

            rs.DisplayId = EditorGUILayout.TextField(GUIContent.none, rs.DisplayId);

            if (oldColor != null)
                GUI.color = (Color)oldColor;

            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        void DoVideoErrorsGUI()
        {
            var boxRect = GUILayoutUtility.GetLastRect();
            var oldColor = GUI.color;
            GUI.color = Color.grey;
            GUI.Box(new Rect(0, boxRect.y + boxRect.height, Screen.width, Screen.height), GUIContent.none);
            GUI.color = oldColor;
            EditorGUILayout.Space(20);
            EditorGUILayout.HelpBox(m_CaptureVideo.Errors, MessageType.Error);
        }
    }
}
