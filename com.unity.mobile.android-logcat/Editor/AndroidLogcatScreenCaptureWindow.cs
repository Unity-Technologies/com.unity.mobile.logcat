using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Linq;

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
        }
        private enum Mode
        {
            Screenshot,
            Video
        }

        [SerializeField]
        private string[] m_ImagePath;
        [SerializeField]
        private Mode m_Mode;

        private AndroidLogcatRuntimeBase m_Runtime;

        private const int kButtonAreaHeight = 30;
        private const int kBottomAreaHeight = 8;
        private AndroidLogcatCaptureScreenshot m_CaptureScreenshot;
        private AndroidLogcatCaptureVideo m_CaptureVideo;
        private AndroidLogcatVideoPlayer m_VideoPlayer;

        private AndroidLogcatDeviceSelection m_DeviceSelection;
        private IAndroidLogcatDevice m_LastDeviceUsedForAssets;

        private bool IsCapturing
        {
            get
            {
                switch (m_Mode)
                {
                    case Mode.Screenshot: return m_CaptureScreenshot.IsCapturing;
                    case Mode.Video: return m_CaptureVideo.IsRecording;
                    default:
                        throw new NotImplementedException(m_Mode.ToString());
                }
            }
        }

        private string TemporaryPath
        {
            get
            {
                switch (m_Mode)
                {
                    case Mode.Screenshot: return m_CaptureScreenshot.GetImagePath(m_DeviceSelection.SelectedDevice);
                    case Mode.Video: return m_CaptureVideo.GetVideoPath(m_DeviceSelection.SelectedDevice);
                    default:
                        throw new NotImplementedException(m_Mode.ToString());
                }
            }
        }

        private string ExtensionForDialog
        {
            get
            {
                return Path.GetExtension(TemporaryPath).Substring(1);
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
            m_DeviceSelection = new AndroidLogcatDeviceSelection(m_Runtime, ReloadCaptureAssetsIfNeeded);
            m_Runtime.Update += OnUpdate;
            m_Runtime.Closing += OnDisable;
            m_CaptureScreenshot = m_Runtime.CaptureScreenshot;
            m_CaptureVideo = m_Runtime.CaptureVideo;
            m_VideoPlayer = new AndroidLogcatVideoPlayer();

            m_Runtime.DeviceQuery.UpdateConnectedDevicesList(true);
        }

        private void OnUpdate()
        {
            m_Runtime.DeviceQuery.UpdateConnectedDevicesList(false);
        }

        private void ReloadCaptureAssetsIfNeeded(IAndroidLogcatDevice device)
        {
            if (m_LastDeviceUsedForAssets == device)
                return;

            m_LastDeviceUsedForAssets = device;

            m_VideoPlayer.Play(m_CaptureVideo.GetVideoPath(device));
            m_Runtime.CaptureScreenshot.LoadImage(m_Runtime.CaptureScreenshot.GetImagePath(device));
        }

        private void OnDisable()
        {
            if (m_VideoPlayer != null)
            {
                m_VideoPlayer.Dispose();
                m_VideoPlayer = null;
            }
            if (!AndroidBridge.AndroidExtensionsInstalled)
                return;

            if (m_Runtime == null)
                return;
            m_Runtime.Update -= OnUpdate;
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

        void DoModeGUI()
        {
            m_Mode = (Mode)EditorGUILayout.EnumPopup(m_Mode, AndroidLogcatStyles.toolbarPopup);
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
            GUIContent statusIcon = GUIContent.none;
            if (IsCapturing)
            {
                int frame = (int)Mathf.Repeat(Time.realtimeSinceStartup * 10, 11.99f);
                statusIcon = AndroidLogcatStyles.Status.GetContent(frame);
                Repaint();
            }
            GUILayout.Label(statusIcon, AndroidLogcatStyles.StatusIcon, GUILayout.Width(30));
        }

        private void DoCaptureGUI()
        {
            EditorGUI.BeginDisabledGroup(m_DeviceSelection.SelectedDevice == null);
            switch (m_Mode)
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
                var length = Enum.GetValues(typeof(Mode)).Length;
                if (m_ImagePath == null || m_ImagePath.Length != length)
                {
                    var defaultDirectory = Path.Combine(Application.dataPath, "..");
                    m_ImagePath = new string[length];
                    for (int i = 0; i < length; i++)
                        m_ImagePath[i] = defaultDirectory;
                }

                var path = EditorUtility.SaveFilePanel("Save Screen Capture", m_ImagePath[(int)m_Mode], Path.GetFileName(TemporaryPath), ExtensionForDialog);
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        m_ImagePath[(int)m_Mode] = path;
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

        private void DoPreviewGUI()
        {
            switch (m_Mode)
            {
                case Mode.Screenshot:
                    {
                        var rc = new Rect(0, kButtonAreaHeight, position.width, position.height - kButtonAreaHeight - kBottomAreaHeight);
                        m_CaptureScreenshot.DoGUI(rc);
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
