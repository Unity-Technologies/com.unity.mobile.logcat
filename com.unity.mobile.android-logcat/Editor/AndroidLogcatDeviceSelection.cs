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
                m_OnNewDeviceSelected?.Invoke(SelectedDevice);
                m_PreviousDeviceSelected = SelectedDevice;
            }
        }

        public void DoGUI()
        {
            var currentSelectedDevice = SelectedDevice == null ? "No device" : SelectedDevice.ShortDisplayName;

            GUILayout.Label(new GUIContent(currentSelectedDevice, "Select android device"), AndroidLogcatStyles.toolbarPopup);

            var rect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                // Only update device list, when we select this UI item
                m_Runtime.DeviceQuery.UpdateConnectedDevicesList(true);

                var names = m_Devices.Select(m => new GUIContent(m.ShortDisplayName)).ToList();

                var selectedIndex = -1;
                for (int i = 0; i < names.Count && currentSelectedDevice != null; i++)
                {
                    if (currentSelectedDevice == names[i].text)
                    {
                        selectedIndex = i;
                        break;
                    }
                }

                EditorUtility.DisplayCustomMenu(new Rect(rect.x, rect.yMax, 0, 0), names.ToArray(), selectedIndex, (userData, options, selected) =>
                {
                    m_SelectedDeviceIdx = selected;
                    m_OnNewDeviceSelected?.Invoke(SelectedDevice);
                    m_PreviousDeviceSelected = SelectedDevice;
                }, null);
            }
        }
    }
}
