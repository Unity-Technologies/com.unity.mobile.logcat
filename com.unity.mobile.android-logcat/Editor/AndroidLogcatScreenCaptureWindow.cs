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
        private string m_ImagePath;
        [SerializeField]
        private Mode m_Mode;

        private AndroidLogcatRuntimeBase m_Runtime;

        private const int kButtonAreaHeight = 30;
        private const int kBottomAreaHeight = 8;
        private AndroidLogcatCaptureScreenshot m_ScreenCapture;

        private IAndroidLogcatDevice[] m_Devices;
        private int m_SelectedDeviceIdx;


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
            m_ScreenCapture = m_Runtime.CaptureScreenshot;

            OnDevicesUpdated();
            ResolveSelectedDeviceIndex();
        }

        private void OnDisable()
        {
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
            m_ScreenCapture.QueueScreenCapture(m_Runtime.DeviceQuery.SelectedDevice, OnCompleted);
        }

        void OnCompleted()
        {
            var texture = m_ScreenCapture.ImageTexture;
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

            EditorGUILayout.BeginHorizontal(AndroidLogcatStyles.toolbar);

            GUIContent statusIcon = GUIContent.none;
            if (m_ScreenCapture.Capturing)
            {
                int frame = (int)Mathf.Repeat(Time.realtimeSinceStartup * 10, 11.99f);
                statusIcon = AndroidLogcatStyles.Status.GetContent(frame);
                Repaint();
            }
            GUILayout.Label(statusIcon, AndroidLogcatStyles.StatusIcon, GUILayout.Width(30));

            EditorGUI.BeginChangeCheck();

            DoSelectedDeviceGUI();

            if (EditorGUI.EndChangeCheck())
                QueueScreenCapture();

            DoModeGUI();


            EditorGUI.BeginDisabledGroup(m_ScreenCapture.Capturing);
            if (GUILayout.Button("Capture", AndroidLogcatStyles.toolbarButton))
                QueueScreenCapture();
            EditorGUI.EndDisabledGroup();

            m_ScreenCapture.DoSaveAsGUI();

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
            if (m_Runtime.DeviceQuery.SelectedDevice == null)
                EditorGUILayout.HelpBox("No valid device detected, please reopen this window after selecting proper device.", MessageType.Info);
            else
            {
                var rc = new Rect(0, kButtonAreaHeight, position.width, position.height - kButtonAreaHeight - kBottomAreaHeight);
                m_ScreenCapture.DoGUI(rc);
            }
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
    }
}
