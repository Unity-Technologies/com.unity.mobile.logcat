using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEditor.ShortcutManagement;

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
            public static GUIContent Open = new GUIContent("Open", "Open the recorded video.");
            public static GUIContent SaveAs = new GUIContent("Save As", "Save the recorded video as a file on your computer.");
            static readonly GUIContent kMore = EditorGUIUtility.IconContent("_Menu");
            public static GUIContent Advanced = kMore != null && kMore.image != null
                ? new GUIContent(kMore.image)
                : new GUIContent("...");

            public static GUIContent TakeScreenshot = new GUIContent("Take Screenshot",
                "Capture the device screen and add it to the list. The screenshot comes from the device "
                + "rather than from the stream, so it is full resolution whatever the stream is scaled to. "
                + "Shortcut: Ctrl+Shift+S, Cmd+Shift+S on macOS, while this view is showing.");
            public static GUIContent CaptureVideo = new GUIContent("Capture", "Record the video from the android device, click Stop afterwards to stop the recording.");
            public static GUIContent StopVideo = new GUIContent("Stop", "Stop the recording.");
        }
        internal enum Mode
        {
            Screenshot,
            Video
        }
        private AndroidLogcatRuntimeBase m_Runtime;

        private const int kButtonAreaHeight = 30;

        private AndroidLogcatCaptureScreenshot m_CaptureScreenshot;
        private AndroidLogcatCaptureVideo m_CaptureVideo;
        private AndroidLogcatVideoPlayer m_VideoPlayer;
        private AndroidLogcatLiveStream m_LiveStream;

        private AndroidLogcatDeviceSelection m_DeviceSelection;
        private IAndroidLogcatDevice m_LastDeviceUsedForAssets;

        private AndroidLogcatScreenshotList m_ScreenshotList;
        private AndroidLogcatStatusBar m_StatusBar;

        // Recording the screen will join this.
        private AndroidLogcatLiveStream.CaptureAction[] m_CaptureActions;

        // Fixed, so that what follows it does not move when the mode changes.
        const float kModeDropdownWidth = 90;
        const float kAdvancedMenuWidth = 26;

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

        /// <summary>
        /// The recording of the selected device. Only video has one: a screenshot is
        /// opened and saved from its own row in the list.
        /// </summary>
        private string VideoPath => m_CaptureVideo.GetVideoPath(m_DeviceSelection.SelectedDevice);

        // Alongside the Logcat window's own entry, and reachable without opening that
        // window first - the Screen Capture window is useful on its own. A device with
        // no Android support installed gets the same message here as anywhere else, from
        // OnGUI, rather than the item being hidden.
        [MenuItem("Window/Analysis/Android Screen Capture")]
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
            m_LiveStream.StreamChanged += ReportStream;
            m_VideoPlayer = new AndroidLogcatVideoPlayer();
            m_ScreenshotList = new AndroidLogcatScreenshotList(m_Runtime, Repaint);
            // Nothing here connects to anything, so the bar carries the message alone.
            m_StatusBar = new AndroidLogcatStatusBar() { ShowConnection = false };

            // The buttons the live view draws for this window, in the order they
            // appear. Each says for itself when it can run.
            m_CaptureActions = new[]
            {
                new AndroidLogcatLiveStream.CaptureAction(Styles.TakeScreenshot, QueueScreenCapture,
                    () => CanCaptureScreenshot)
            };

            // Settings saved while the removed LiveStream mode was selected still hold
            // its value, which is now out of range and would throw in the switches above.
            var captureSettings = m_Runtime.UserSettings.CaptureSettings;
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

            // The screenshots are not tied to a device, so losing one keeps the view.
            if (string.IsNullOrEmpty(m_Runtime.CaptureScreenshot.SelectedImagePath))
                m_Runtime.CaptureScreenshot.LoadImage(m_Runtime.CaptureScreenshot.GetLatestImagePath(device));

            m_ScreenshotList.OnDeviceChanged(m_DeviceSelection.SelectedDevice);
        }

        private void OnDisable()
        {
            // The live stream is owned by the runtime, so it would otherwise keep
            // mirroring the device after the window that was showing it is gone - and
            // keep reporting to a status bar that is gone with it.
            m_ScreenshotList?.Deselect();

            if (m_LiveStream != null)
            {
                m_LiveStream.StreamChanged -= ReportStream;
                m_LiveStream = null;
            }

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

        /// <summary>
        /// Whether a screenshot can be taken right now. Take Screenshot is drawn in the
        /// live view and nowhere else, so the shortcut goes where the button goes.
        /// </summary>
        private bool CanCaptureScreenshot =>
            m_ScreenshotList != null && m_ScreenshotList.LiveSelected
            && m_DeviceSelection.SelectedDevice != null && !m_CaptureScreenshot.IsCapturing;

        private void QueueScreenCapture()
        {
            // Whatever the bar said about the last one is about to be out of date.
            m_StatusBar.Message = string.Empty;
            m_CaptureScreenshot.QueueScreenCapture(m_DeviceSelection.SelectedDevice, OnScreenshotCompleted);
        }

        /// <summary>
        /// Says what the stream is doing, whenever it has something new to say - see
        /// <see cref="AndroidLogcatLiveStream.StreamChanged"/>.
        /// </summary>
        private void ReportStream()
        {
            var stream = m_LiveStream.StreamSize;

            if (!m_LiveStream.IsStreaming)
            {
                // The sizes outlive the stream, so a stream that ended having never
                // delivered a frame has nothing here. It failed, and the view says so
                // where the image would be.
                if (stream.x > 0)
                    m_StatusBar.Message = "Live stream stopped";
            }
            else
            {
                var display = m_LiveStream.DisplaySize;
                var scaledFrom = display.x > 0 ? $"{display.x}x{display.y} scaled to " : string.Empty;
                var device = m_DeviceSelection.SelectedDevice;
                var name = device != null ? device.ShortDisplayName : "device";

                m_StatusBar.Message = $"Live stream: {name}, {scaledFrom}{stream.x}x{stream.y}, " +
                    $"up to {m_Runtime.Settings.LiveStreamMaxFps} fps";
            }

            Repaint();
        }

        /// <summary>
        /// Says where a capture landed, in the status bar. A path inside the project
        /// is shown relative to it, which is short enough to read at a glance.
        /// </summary>
        private void ReportSaved(string what, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                m_StatusBar.Message = string.Empty;
                return;
            }

            var full = Path.GetFullPath(path).Replace("\\", "/");
            var project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace("\\", "/") + "/";
            var shown = full.StartsWith(project, StringComparison.OrdinalIgnoreCase)
                ? full.Substring(project.Length)
                : full;

            m_StatusBar.Message = $"{what} saved to '{shown}'";
        }

        /// <summary>
        /// Ctrl+Shift+S, and Cmd+Shift+S on macOS - <see cref="ShortcutModifiers.Action"/>
        /// is whichever of the two the platform uses.
        /// <para>
        /// Scoped to this window rather than registered globally: the Editor's own
        /// File > Save As sits on the same chord, and a window scoped shortcut takes
        /// precedence over a global one only while its window has focus. It shows up in
        /// Edit > Shortcuts under "Android Logcat", so it can be rebound there.
        /// </para>
        /// </summary>
        [Shortcut("Android Logcat/Capture Screenshot", typeof(AndroidLogcatScreenCaptureWindow),
            KeyCode.S, ShortcutModifiers.Action | ShortcutModifiers.Shift)]
        static void CaptureScreenshotShortcut(ShortcutArguments args)
        {
            var window = args.context as AndroidLogcatScreenCaptureWindow;
            if (window != null)
                window.CaptureScreenshotFromShortcut();
        }

        void CaptureScreenshotFromShortcut()
        {
            // The button's own conditions, plus the mode: Video mode has no live view.
            if (m_Runtime == null || m_DeviceSelection == null)
                return;
            if (m_Runtime.UserSettings.CaptureSettings.Mode != Mode.Screenshot)
                return;
            if (!CanCaptureScreenshot)
                return;

            QueueScreenCapture();
        }

        void OnScreenshotCompleted()
        {
            // The image lands on disk while the capture is still running, and its
            // details file only when the capture is integrated here. Selecting the row
            // in between loads one without the other, and the preview would keep that
            // for as long as the selection does not change.
            m_ScreenshotList?.InvalidatePreview();

            // Set after the capture was integrated, so this is the new screenshot -
            // and empty when the capture failed, where the error is reported already.
            var captured = m_CaptureScreenshot.SelectedImagePath;
            ReportSaved("Screenshot", captured);
            m_ScreenshotList?.Flash(captured);

            var texture = m_CaptureScreenshot.ImageTexture;
            if (texture != null)
                maxSize = new Vector2(Math.Max(texture.width, position.width), texture.height + kButtonAreaHeight);
            Repaint();
        }

        void OnVideoCompleted(AndroidLogcatCaptureVideo.Result result, string videoPath)
        {
            ReportSaved("Video", result == AndroidLogcatCaptureVideo.Result.Success ? videoPath : null);

            if (result == AndroidLogcatCaptureVideo.Result.Success)
                m_VideoPlayer.Play(videoPath);
            Repaint();
        }

        void DoModeGUI()
        {
            var settings = m_Runtime.UserSettings.CaptureSettings;
            var mode = (Mode)EditorGUILayout.EnumPopup(settings.Mode, AndroidLogcatStyles.toolbarPopup,
                GUILayout.Width(kModeDropdownWidth));
            if (mode == settings.Mode)
                return;

            settings.Mode = mode;

            // The list, and with it the Live row, is only drawn in Screenshot mode.
            // Leaving that mode has to stop the stream, or the server carries on
            // mirroring the device's display for a window that no longer shows it -
            // and Video mode would happily start a recording alongside it.
            if (mode != Mode.Screenshot)
                m_ScreenshotList.Deselect();
        }

        /// <summary>
        /// The screenshots folder is an ordinary directory that the user can add to,
        /// delete from or overwrite behind the Editor's back. Nothing inside the Editor
        /// can notice that, so the listing and the loaded image are both dropped when
        /// this window comes back to the front - the moment someone is most likely to
        /// have just been doing exactly that in a file browser.
        /// </summary>
        void OnFocus()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled || m_Runtime == null)
                return;

            m_CaptureScreenshot.InvalidateScreenshots();
            m_ScreenshotList?.InvalidatePreview();
            Repaint();
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

            GUILayout.Space(5);
            DoPreviewGUI();

            // Video mode's settings take only the height they need, so the bar would
            // sit under the last control rather than at the bottom of the window.
            // Screenshot mode claims what is left for the list and the preview, and
            // must not be made to share it.
            if (m_Runtime.UserSettings.CaptureSettings.Mode == Mode.Video)
                GUILayout.FlexibleSpace();

            m_StatusBar?.DoGUI();

            EditorGUILayout.EndVertical();
        }

        private void DoToolbarGUI()
        {
            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);

            DoProgressGUI();
            m_DeviceSelection.DoGUI();

            DoModeGUI();
            DoCaptureGUI();

            // Remove this once Live view is reimplemented in Video mode, or the button is moved to the video player.
            if (m_Runtime.UserSettings.CaptureSettings.Mode == Mode.Video)
            {
                DoOpenGUI();
                DoSaveAsGUI();
            }

            GUILayout.FlexibleSpace();
            DoAdvancedMenuGUI();

            EditorGUILayout.EndHorizontal();

            var toolbarRect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(new Rect(toolbarRect.x, toolbarRect.yMax - 1, toolbarRect.width, 1),
                    EditorGUIUtility.isProSkin ? new Color(0.14f, 0.14f, 0.14f) : new Color(0.6f, 0.6f, 0.6f));
            }
        }

        /// <summary>
        /// The settings this window has, reached the way the Package Manager's toolbar
        /// reaches its own: a menu at the right hand end of the toolbar.
        /// </summary>
        private void DoAdvancedMenuGUI()
        {
            var rect = GUILayoutUtility.GetRect(Styles.Advanced, AndroidLogcatStyles.toolbarButton,
                GUILayout.Width(kAdvancedMenuWidth));
            if (!GUI.Button(rect, Styles.Advanced, AndroidLogcatStyles.toolbarButton))
                return;

            var menu = new GenericMenu();
            menu.AddItem(EditorGUIUtility.TrTextContent("Preferences"), false,
                () => SettingsService.OpenUserPreferences(AndroidLogcatSettingsProvider.kSettingsPath));
            menu.DropDown(rect);
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
                    // Taking a screenshot lives in the live view, beside the screen it
                    // captures - see DoScreenshotGUI.
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

                            m_StatusBar.Message = string.Empty;
                            m_CaptureVideo.StartRecording(m_DeviceSelection.SelectedDevice, OnVideoCompleted, timeLimit, videoSizeX, videoSizeY, bitRate, displayId);
                        }
                    }
                    break;
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DoOpenGUI()
        {
            EditorGUI.BeginDisabledGroup(!File.Exists(VideoPath));
            if (GUILayout.Button(Styles.Open, AndroidLogcatStyles.toolbarButton))
                AndroidLogcatUtilities.OpenFile(VideoPath);
            EditorGUI.EndDisabledGroup();
        }

        private void DoSaveAsGUI()
        {
            EditorGUI.BeginDisabledGroup(!File.Exists(VideoPath));
            if (GUILayout.Button(Styles.SaveAs, AndroidLogcatStyles.toolbarButton))
            {
                var settings = m_Runtime.UserSettings.CaptureSettings;
                settings.SaveFileAs(settings.Mode, VideoPath, "Save Screen Capture");
            }
            EditorGUI.EndDisabledGroup();
        }

        /// <summary>
        /// The list of saved screenshots on the left, the selected one on the right, a
        /// draggable splitter between them.
        /// </summary>
        private void DoScreenshotGUI(Rect rc)
        {
            // Drawn with or without a device: these are files on this machine, and
            // they outlive the device they came from. What needs a device - Capture,
            // the live view - disables itself.
            // The list draws itself and the splitter, and hands back what is left.
            var imageRect = m_ScreenshotList.DoGUI(rc, m_DeviceSelection.SelectedDevice);

            if (m_ScreenshotList.LiveSelected)
            {
                // The developer-mode details are drawn by DoGUI, in the info column.
                m_LiveStream.DoGUI(imageRect, m_DeviceSelection.SelectedDevice, Repaint, m_CaptureActions);
                // Frames arrive on the runtime's update, not on GUI events, so the window
                // has to keep repainting to show them.
                if (m_LiveStream.IsStreaming)
                    Repaint();
            }
            // The list draws the image, not AndroidLogcatCaptureScreenshot: its texture
            // is the last capture rather than the selected row.
            else if (!m_ScreenshotList.DoPreviewGUI(imageRect))
            {
                var message = m_DeviceSelection.SelectedDevice == null
                    ? "No screenshot to show. Select one from the list."
                    : "No screenshot to show. Select Capture to take one.";
                EditorGUI.HelpBox(imageRect, message, MessageType.Info);
            }
        }

        private void DoPreviewGUI()
        {
            switch (m_Runtime.UserSettings.CaptureSettings.Mode)
            {
                case Mode.Screenshot:
                    {
                        // Claimed from the layout rather than offset by a hardcoded
                        // toolbar height, which left a gap when the two disagreed.
                        var rc = GUILayoutUtility.GetRect(0, 0,
                            GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                        DoScreenshotGUI(rc);
                    }
                    break;
                case Mode.Video:
                    // Unlike the saved screenshots, a recording belongs to the device it
                    // was taken from and is kept per device, so there is nothing to show
                    // while none is selected.
                    if (m_DeviceSelection.SelectedDevice == null)
                    {
                        EditorGUILayout.HelpBox(
                            "No device selected. Connect a device, then select it from the device list.",
                            MessageType.Info);
                        break;
                    }

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
