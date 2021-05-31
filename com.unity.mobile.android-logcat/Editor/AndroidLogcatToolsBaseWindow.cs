using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatToolsBaseWindow : EditorWindow
    {
        protected const float kSaveButtonWidth = 100;
        protected AndroidLogcatRuntimeBase m_Runtime;
        private IAndroidLogcatDevice[] m_Devices;
        private int m_SelectedDevice;


        protected virtual void OnEnable()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
                return;

            m_Runtime = AndroidLogcatManager.instance.Runtime;
            m_Runtime.DeviceQuery.DevicesUpdated += DeviceQuery_DevicesUpdated;

            DeviceQuery_DevicesUpdated();

            if (m_Runtime.DeviceQuery.SelectedDevice != null)
            {
                var id = m_Runtime.DeviceQuery.SelectedDevice.Id;
                for (int i = 0; i < m_Devices.Length; i++)
                {
                    if (id == m_Devices[i].Id)
                    {
                        m_SelectedDevice = i;
                        break;
                    }
                }
            }

            m_Runtime.Closing += OnDisable;
        }

        protected virtual void OnDisable()
        {
            if (m_Runtime == null)
                return;
            if (!AndroidBridge.AndroidExtensionsInstalled)
                return;

            m_Runtime.DeviceQuery.DevicesUpdated -= DeviceQuery_DevicesUpdated;
            m_SelectedDevice = 0;
            m_Runtime = null;
        }

        private void DeviceQuery_DevicesUpdated()
        {
            m_Devices = m_Runtime.DeviceQuery.Devices.Where(m => m.Value.State == IAndroidLogcatDevice.DeviceState.Connected).Select(m => m.Value).ToArray();
        }

        protected IAndroidLogcatDevice SelectedDevice
        {
            get
            {
                if (m_SelectedDevice < 0 || m_SelectedDevice > m_Devices.Length - 1)
                    return null;
                return m_Devices[m_SelectedDevice];
            }
        }

        protected string GetDeviceId()
        {
            if (m_SelectedDevice < 0 || m_SelectedDevice > m_Devices.Length - 1)
                return string.Empty;
            return m_Devices[m_SelectedDevice].Id;
        }

        protected void DoProgressGUI(bool spin)
        {
            GUIContent statusIcon = GUIContent.none;
            if (spin)
            {
                int frame = (int)Mathf.Repeat(Time.realtimeSinceStartup * 10, 11.99f);
                statusIcon = AndroidLogcatStyles.Status.GetContent(frame);
                Repaint();
            }
            GUILayout.Label(statusIcon, AndroidLogcatStyles.StatusIcon, GUILayout.Width(30));
        }

        protected void DoSelectedDeviceGUI()
        {
            m_SelectedDevice = EditorGUILayout.Popup(m_SelectedDevice,
                m_Devices.Select(m => new GUIContent(m.Id)).ToArray(),
                AndroidLogcatStyles.toolbarPopup,
                GUILayout.MaxWidth(300));
        }

        protected bool DoIsSupportedGUI()
        {
            if (!AndroidBridge.AndroidExtensionsInstalled)
            {
                AndroidLogcatUtilities.ShowAndroidIsNotInstalledMessage();
                return false;
            }

            return true;
        }
    }
}
