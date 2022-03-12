using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatScreenCaptureWindow : EditorWindow
    {
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

        private IAndroidLogcatDevice[] m_Devices;
        private int m_SelectedDeviceIdx;

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
                    case Mode.Screenshot: return m_CaptureScreenshot.ImagePath;
                    case Mode.Video: return m_CaptureVideo.VideoPath;
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
            AndroidLogcatScreenCaptureWindow win = EditorWindow.GetWindow<AndroidLogcatScreenCaptureWindow>("Device Screen Capture");
            win.QueueScreenCapture();
        }

        private void OnEnable()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
                return;

            m_Runtime = AndroidLogcatManager.instance.Runtime;
            m_Runtime.DeviceQuery.DevicesUpdated += OnDevicesUpdated;
            m_Runtime.Closing += OnDisable;
            m_CaptureScreenshot = m_Runtime.CaptureScreenshot;
            m_CaptureVideo = m_Runtime.CaptureVideo;
            m_VideoPlayer = new AndroidLogcatVideoPlayer();

            OnDevicesUpdated();
            ResolveSelectedDeviceIndex();
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
            m_Runtime.DeviceQuery.DevicesUpdated -= OnDevicesUpdated;
            m_Runtime = null;
        }

        private void ResolveSelectedDeviceIndex()
        {
            if (m_Runtime.DeviceQuery.SelectedDevice == null)
                return;

            var id = m_Runtime.DeviceQuery.SelectedDevice.Id;
            for (int i = 0; i < m_Devices.Length; i++)
            {
                if (id == m_Devices[i].Id)
                {
                    m_SelectedDeviceIdx = i;
                    break;
                }
            }
        }

        private void OnDevicesUpdated()
        {
            m_Devices = m_Runtime.DeviceQuery.Devices.Where(m => m.Value.State == IAndroidLogcatDevice.DeviceState.Connected).Select(m => m.Value).ToArray();
        }

        protected IAndroidLogcatDevice SelectedDevice
        {
            get
            {
                if (m_SelectedDeviceIdx < 0 || m_SelectedDeviceIdx > m_Devices.Length - 1)
                    return null;
                return m_Devices[m_SelectedDeviceIdx];
            }
        }

        private void QueueScreenCapture()
        {
            m_CaptureScreenshot.QueueScreenCapture(m_Runtime.DeviceQuery.SelectedDevice, OnCompleted);
        }

        void OnCompleted()
        {
            var texture = m_CaptureScreenshot.ImageTexture;
            if (texture != null)
                maxSize = new Vector2(Math.Max(texture.width, position.width), texture.height + kButtonAreaHeight);
            Repaint();
        }

        private void DoSelectedDeviceGUI()
        {
            m_SelectedDeviceIdx = EditorGUILayout.Popup(m_SelectedDeviceIdx,
                m_Devices.Select(m => new GUIContent(m.Id)).ToArray(),
                AndroidLogcatStyles.toolbarPopup,
                GUILayout.MaxWidth(300));
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
            if (m_Runtime.DeviceQuery.SelectedDevice == null)
                EditorGUILayout.HelpBox("No valid device selected.", MessageType.Info);
            else
                DoPreviewGUI();
            EditorGUILayout.EndVertical();

            /*
                        var rs = m_Runtime.UserSettings.RecorderSettings;
                        if (rs.VideoSizeEnabled)
                            args += $" --size {rs.VideoSizeX}x{rs.VideoSizeY}";
                        if (rs.BitRateEnabled)
                            args += $" --bit-rate {rs.BitRate}";
                        if (rs.DisplayIdEnabled)
                            args += $" --display-id {rs.DisplayId}";
                        args += $" {kVideoPathOnDevice}";
            */
        }

        private void DoToolbarGUI()
        {
            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);

            DoProgressGUI();

            EditorGUI.BeginChangeCheck();

            DoSelectedDeviceGUI();

            if (EditorGUI.EndChangeCheck())
            {
                switch (m_Mode)
                {
                    // We switched the device, do screenshot automatically, since it's nice
                    case Mode.Screenshot:
                        QueueScreenCapture();
                        break;
                    case Mode.Video:
                        // Do nothing
                        break;
                }
            }

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
            switch (m_Mode)
            {
                case Mode.Screenshot:
                    EditorGUI.BeginDisabledGroup(m_CaptureScreenshot.IsCapturing);
                    if (GUILayout.Button("Capture", AndroidLogcatStyles.toolbarButton))
                        QueueScreenCapture();
                    EditorGUI.EndDisabledGroup();
                    break;
                case Mode.Video:
                    if (m_CaptureVideo.IsRecording)
                    {
                        if (GUILayout.Button("Stop", AndroidLogcatStyles.toolbarButton))
                        {
                            m_CaptureVideo.StopRecording();
                            m_VideoPlayer.Play(m_CaptureVideo.VideoPath);
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Capture", AndroidLogcatStyles.toolbarButton))
                        {
                            // TODO settings;
                            m_CaptureVideo.StartRecording(SelectedDevice);
                        }
                    }
                    break;
            }
        }

        private void DoOpenGUI()
        {
            EditorGUI.BeginDisabledGroup(!File.Exists(TemporaryPath));
            if (GUILayout.Button("Open", AndroidLogcatStyles.toolbarButton))
                Application.OpenURL(TemporaryPath);
            EditorGUI.EndDisabledGroup();
        }

        private void DoSaveAsGUI()
        {
            EditorGUI.BeginDisabledGroup(!File.Exists(TemporaryPath));
            if (GUILayout.Button("Save As", AndroidLogcatStyles.toolbarButton))
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
                    m_VideoPlayer.DoGUI();
                    if (m_VideoPlayer.IsPlaying())
                        Repaint();
                    // TODO:
                    break;
            }
        }
    }
}
