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
            public static GUIContent Open = new GUIContent("Open", "Open captured screenshot or video.");
            public static GUIContent SaveAs = new GUIContent("Save As", "Save captured screenshot or video.");
            public static GUIContent CaptureScreenshot = new GUIContent("Capture",
                "Capture screenshot from the android device. Shortcut: Ctrl+Shift+S, Cmd+Shift+S on macOS.");
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
                    case Mode.Screenshot: return m_ScreenshotList.LiveSelected ? string.Empty : m_CaptureScreenshot.SelectedImagePath;
                    case Mode.Video: return m_CaptureVideo.GetVideoPath(m_DeviceSelection.SelectedDevice);
                    default:
                        throw new NotImplementedException(mode.ToString());
                }
            }
        }

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
            m_VideoPlayer = new AndroidLogcatVideoPlayer();
            m_ScreenshotList = new AndroidLogcatScreenshotList(m_Runtime, Repaint);

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
            m_Runtime.CaptureScreenshot.LoadImage(m_Runtime.CaptureScreenshot.GetLatestImagePath(device));

            m_ScreenshotList.OnDeviceChanged(m_DeviceSelection.SelectedDevice);
        }

        private void OnDisable()
        {
            // The live stream is owned by the runtime, so it would otherwise keep
            // mirroring the device after the window that was showing it is gone.
            m_ScreenshotList?.Deselect();

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
            // The same conditions the Capture button draws itself with: it is disabled
            // without a device and while a capture is in flight, and in Video mode it
            // records video instead, which this shortcut is not for.
            if (m_Runtime == null || m_DeviceSelection == null)
                return;
            if (m_Runtime.UserSettings.CaptureSettings.Mode != Mode.Screenshot)
                return;
            if (m_DeviceSelection.SelectedDevice == null || m_CaptureScreenshot.IsCapturing)
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

        void DoModeGUI()
        {
            var settings = m_Runtime.UserSettings.CaptureSettings;
            var mode = (Mode)EditorGUILayout.EnumPopup(settings.Mode, AndroidLogcatStyles.toolbarPopup);
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
                AndroidLogcatUtilities.OpenFile(TemporaryPath);
            EditorGUI.EndDisabledGroup();
        }

        private void DoSaveAsGUI()
        {
            EditorGUI.BeginDisabledGroup(!File.Exists(TemporaryPath));
            if (GUILayout.Button(Styles.SaveAs, AndroidLogcatStyles.toolbarButton))
            {
                var settings = m_Runtime.UserSettings.CaptureSettings;
                var mode = settings.Mode;
                var directory = AndroidLogcatUtilities.SaveFileAs(TemporaryPath, "Save Screen Capture",
                    settings.GetLastSaveLocation(mode));
                if (directory != null)
                    settings.SetLastSaveLocation(mode, directory);
            }
            EditorGUI.EndDisabledGroup();
        }

        /// <summary>
        /// The list of saved screenshots on the left, the selected one on the right, a
        /// draggable splitter between them.
        /// </summary>
        private void DoScreenshotGUI(Rect rc)
        {
            // The list draws itself and the splitter, and hands back what is left.
            var imageRect = m_ScreenshotList.DoGUI(rc, m_DeviceSelection.SelectedDevice);

            if (m_ScreenshotList.LiveSelected)
            {
                // The developer-mode details are drawn by DoGUI, in the info column.
                m_LiveStream.DoGUI(imageRect, m_DeviceSelection.SelectedDevice, Repaint);
                // Frames arrive on the runtime's update, not on GUI events, so the window
                // has to keep repainting to show them.
                if (m_LiveStream.IsStreaming)
                    Repaint();
            }
            // The list draws the image, not AndroidLogcatCaptureScreenshot: its texture
            // belongs to the Layout Viewer as much as to this window.
            else if (!m_ScreenshotList.DoPreviewGUI(imageRect))
            {
                var message = m_DeviceSelection.SelectedDevice == null
                    ? "No screenshot to show."
                    : "No screenshot to show, click Capture button.";
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
