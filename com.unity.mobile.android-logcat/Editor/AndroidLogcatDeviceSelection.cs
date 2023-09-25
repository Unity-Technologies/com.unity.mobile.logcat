using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.Android.Logcat
{
    /// <summary>
    /// Independent device selection from device query
    /// </summary>
    class AndroidLogcatDeviceSelection : IDisposable
    {
        AndroidLogcatRuntimeBase m_Runtime;
        IAndroidLogcatDevice[] m_Devices;
        int m_SelectedDeviceIdx;
        IAndroidLogcatDevice m_PreviousDeviceSelected;
        Action<IAndroidLogcatDevice> m_OnNewDeviceSelected;

        public IAndroidLogcatDevice SelectedDevice
        {
            get
            {
                if (m_SelectedDeviceIdx < 0 || m_SelectedDeviceIdx > m_Devices.Length - 1)
                    return null;
                return m_Devices[m_SelectedDeviceIdx];
            }
        }

        public AndroidLogcatDeviceSelection(AndroidLogcatRuntimeBase runtime, Action<IAndroidLogcatDevice> onNewDeviceSelected)
        {
            m_Runtime = runtime;
            m_OnNewDeviceSelected = onNewDeviceSelected;
            m_Runtime.DeviceQuery.DevicesUpdated += OnDevicesUpdated;
            QueryDevices();
        }

        public void Dispose()
        {
            m_Runtime.DeviceQuery.DevicesUpdated -= OnDevicesUpdated;
        }

        private void QueryDevices()
        {
            m_Devices = m_Runtime.DeviceQuery.Devices.Where(m => m.Value.State == IAndroidLogcatDevice.DeviceState.Connected).Select(m => m.Value).ToArray();
            if (m_Devices.Length == 0)
                m_SelectedDeviceIdx = -1;
            else
            {
                m_SelectedDeviceIdx = Math.Min(m_SelectedDeviceIdx, m_Devices.Length - 1);
                if (m_SelectedDeviceIdx < 0)
                    m_SelectedDeviceIdx = 0;
            }
        }

        private void OnDevicesUpdated()
        {
            QueryDevices();
            if (SelectedDevice != m_PreviousDeviceSelected)
            {
                m_OnNewDeviceSelected.Invoke(SelectedDevice);
                m_PreviousDeviceSelected = SelectedDevice;
            }
        }

        public void DoGUI()
        {
            var deviceNames = m_Devices.Select(m => new GUIContent(m.ShortDisplayName)).ToArray();
            if (deviceNames.Length == 0)
            {
                m_SelectedDeviceIdx = 0;
                deviceNames = new[] { new GUIContent("No Device") };
            }
            EditorGUI.BeginChangeCheck();
            m_SelectedDeviceIdx = EditorGUILayout.Popup(m_SelectedDeviceIdx,
                deviceNames,
                AndroidLogcatStyles.toolbarPopup,
                GUILayout.MaxWidth(300));
            if (EditorGUI.EndChangeCheck())
            {
                m_OnNewDeviceSelected.Invoke(SelectedDevice);
                m_PreviousDeviceSelected = SelectedDevice;
            }
        }
    }
}
