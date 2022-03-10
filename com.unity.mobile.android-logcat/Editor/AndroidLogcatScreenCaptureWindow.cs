using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatScreenCaptureWindow : EditorWindow
    {
        [SerializeField] private string m_ImagePath;
        private AndroidLogcatRuntimeBase m_Runtime;
        private GUIContent[] m_Devices;
        private int m_SelectedDevice;

        private const int kButtonAreaHeight = 30;
        private const int kBottomAreaHeight = 8;
        private AndroidLogcatScreenCapture m_ScreenCapture;


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
            m_Runtime.DeviceQuery.DevicesUpdated += DeviceQuery_DevicesUpdated;
            m_ScreenCapture = m_Runtime.ScreenCapture;

            DeviceQuery_DevicesUpdated();

            if (m_Runtime.DeviceQuery.SelectedDevice != null)
            {
                var id = m_Runtime.DeviceQuery.SelectedDevice.Id;
                for (int i = 0; i < m_Devices.Length; i++)
                {
                    if (id == m_Devices[i].text)
                    {
                        m_SelectedDevice = i;
                        break;
                    }
                }
            }
        }

        private void OnDisable()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
                return;

            m_Runtime.DeviceQuery.DevicesUpdated -= DeviceQuery_DevicesUpdated;
            m_SelectedDevice = 0;
        }

        private void DeviceQuery_DevicesUpdated()
        {
            m_Devices = m_Runtime.DeviceQuery.Devices.Where(m => m.Value.State == IAndroidLogcatDevice.DeviceState.Connected)
                .Select(m => new GUIContent(m.Value.Id)).ToArray();
        }

        private string GetDeviceId()
        {
            if (m_SelectedDevice < 0 || m_SelectedDevice > m_Devices.Length - 1)
                return string.Empty;
            return m_Devices[m_SelectedDevice].text;
        }

        private void QueueScreenCapture()
        {
            var id = GetDeviceId();
            if (string.IsNullOrEmpty(id))
                return;

            m_ScreenCapture.QueueScreenCapture(m_Runtime.DeviceQuery.SelectedDevice, OnCompleted);
        }

        void OnCompleted()
        {
            var texture = m_ScreenCapture.ImageTexture;
            if (texture != null)
                maxSize = new Vector2(Math.Max(texture.width, position.width), texture.height + kButtonAreaHeight);
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
            m_SelectedDevice = EditorGUILayout.Popup(m_SelectedDevice, m_Devices, AndroidLogcatStyles.toolbarPopup);
            if (EditorGUI.EndChangeCheck())
                QueueScreenCapture();

            EditorGUI.BeginDisabledGroup(m_ScreenCapture.Capturing);
            if (GUILayout.Button("Capture", AndroidLogcatStyles.toolbarButton))
                QueueScreenCapture();
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("Save...", AndroidLogcatStyles.toolbarButton))
            {
                var path = EditorUtility.SaveFilePanel("Save Screen Capture", "", Path.GetFileName(m_ImagePath), "png");
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        File.Copy(m_ImagePath, path, true);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogErrorFormat("Failed to save to '{0}' as '{1}'.", path, ex.Message);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);
            var id = GetDeviceId();
            if (string.IsNullOrEmpty(id))
                EditorGUILayout.HelpBox("No valid device detected, please reopen this window after selecting proper device.", MessageType.Info);
            else
            {
                var rc = new Rect(0, kButtonAreaHeight, position.width, position.height - kButtonAreaHeight - kBottomAreaHeight);
                m_ScreenCapture.DoGUI(rc);
            }
            EditorGUILayout.EndVertical();
        }
    }
}
