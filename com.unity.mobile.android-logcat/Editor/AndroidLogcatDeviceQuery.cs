#if PLATFORM_ANDROID
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Android;

namespace Unity.Android.Logcat
{
    internal class AndroidLogcatRetrieveDeviceIdsInput : IAndroidLogcatTaskInput
    {
        internal ADB adb;
        internal bool notifyListeners;
    }

    internal class AndroidLogcatRetrieveDeviceIdsResult : IAndroidLogcatTaskResult
    {
        internal List<string> deviceIds = new List<string>();
        internal bool notifyListeners;
    }


    class AndroidLogcatDeviceQuery
    {
        private const int kMillisecondsBetweenConsecutiveDeviceChecks = 1000;

        private IAndroidLogcatDevice m_SelectedDevice;
        private Dictionary<string, IAndroidLogcatDevice> m_Devices = new Dictionary<string, IAndroidLogcatDevice>();
        private IAndroidLogcatRuntime m_Runtime;
        private DateTime m_TimeOfLastDeviceListUpdate;

        public event Action<IAndroidLogcatDevice> DeviceSelected;

        internal IAndroidLogcatDevice SelectedDevice
        {
            get
            {
                return m_SelectedDevice;
            }
        }

        internal IReadOnlyDictionary<string, IAndroidLogcatDevice> Devices
        {
            get
            {
                return m_Devices;
            }
        }

        internal IAndroidLogcatDevice FirstConnectedDevice
        {
            get
            {
                if (m_Devices.Count == 0)
                    return null;
                return m_Devices.First().Value;
            }
        }

        internal AndroidLogcatDeviceQuery(IAndroidLogcatRuntime runtime)
        {
            m_Runtime = runtime;
            m_TimeOfLastDeviceListUpdate = DateTime.Now;
        }

        internal void Clear()
        {
            m_SelectedDevice = null;
        }

        internal void UpdateConnectedDevicesList(bool synchronous, bool notifyListeners = true)
        {
            if ((DateTime.Now - m_TimeOfLastDeviceListUpdate).TotalMilliseconds < kMillisecondsBetweenConsecutiveDeviceChecks && !synchronous)
                return;
            m_TimeOfLastDeviceListUpdate = DateTime.Now;

            m_Runtime.Dispatcher.Schedule(new AndroidLogcatRetrieveDeviceIdsInput() { adb = m_Runtime.Tools.ADB, notifyListeners = notifyListeners }, QueryDevicesAsync, IntegrateQueryDevices, synchronous);
        }

        internal void SelectDevice(IAndroidLogcatDevice device, bool notifyListeners = true)
        {
            if (m_SelectedDevice == device)
                return;

            m_SelectedDevice = device;

            if (m_SelectedDevice != null && !m_Devices.Keys.Contains(m_SelectedDevice.Id))
                throw new Exception("Selected device is not among our listed devices");

            if (notifyListeners && DeviceSelected != null)
                DeviceSelected.Invoke(m_SelectedDevice);
        }

        private static IAndroidLogcatTaskResult QueryDevicesAsync(IAndroidLogcatTaskInput input)
        {
            var adb = ((AndroidLogcatRetrieveDeviceIdsInput)input).adb;

            if (adb == null)
                throw new NullReferenceException("ADB interface has to be valid");

            var result = new AndroidLogcatRetrieveDeviceIdsResult();
            result.notifyListeners = ((AndroidLogcatRetrieveDeviceIdsInput)input).notifyListeners;

            AndroidLogcatInternalLog.Log("{0} devices", adb.GetADBPath());
            try
            {
                var adbOutput = adb.Run(new[] { "devices" }, "Unable to list connected devices. ");
                foreach (var line in adbOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()))
                {
                    AndroidLogcatInternalLog.Log(" " + line);
                    if (line.EndsWith("device"))
                    {
                        var deviceId = line.Split(new[] { '\t', ' ' })[0];
                        result.deviceIds.Add(deviceId);
                    }
                }
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log(ex.Message);
                result.deviceIds = new List<string>();
            }

            return result;
        }

        private void IntegrateQueryDevices(IAndroidLogcatTaskResult resut)
        {
            var deviceIdsResult = ((AndroidLogcatRetrieveDeviceIdsResult)resut);
            var deviceIds = deviceIdsResult.deviceIds;

            // If our selected device was removed, deselect it
            if (m_SelectedDevice != null && !deviceIds.Contains(m_SelectedDevice.Id))
            {
                m_SelectedDevice = null;
                if (deviceIdsResult.notifyListeners && DeviceSelected != null)
                    DeviceSelected.Invoke(m_SelectedDevice);
            }

            // Gather devices we need to remove
            var deviceIdsToRemove = new List<string>();
            foreach (var device in m_Devices)
            {
                if (deviceIds.Contains(device.Value.Id))
                    continue;
                deviceIdsToRemove.Add(device.Value.Id);
            }

            foreach (var toRemove in deviceIdsToRemove)
            {
                m_Devices.Remove(toRemove);
            }

            // Create missing devices
            foreach (var id in deviceIds)
            {
                GetOrCreateDevice(id);
            }


            if (m_SelectedDevice != null)
            {
                if (m_SelectedDevice != m_Devices[m_SelectedDevice.Id])
                    throw new Exception("The selected device is not among our list of devices");
            }
        }

        internal IAndroidLogcatDevice GetDevice(string deviceId)
        {
            IAndroidLogcatDevice device;
            if (m_Devices.TryGetValue(deviceId, out device))
            {
                return device;
            }
            return null;
        }

        private IAndroidLogcatDevice GetOrCreateDevice(string deviceId)
        {
            IAndroidLogcatDevice device;
            if (m_Devices.TryGetValue(deviceId, out device))
            {
                return device;
            }
            device = new AndroidLogcatDevice(m_Runtime.Tools.ADB, deviceId);
            m_Devices[deviceId] = device;

            return device;
        }
    }
}
#endif
