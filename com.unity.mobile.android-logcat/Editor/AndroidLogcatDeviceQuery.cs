#if PLATFORM_ANDROID
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
        internal struct DeviceInfo
        {
            internal string id;
            internal IAndroidLogcatDevice.DeviceState state;
        }

        internal List<DeviceInfo> deviceInfo = new List<DeviceInfo>();
        internal bool notifyListeners;
    }


    class AndroidLogcatDeviceQuery
    {
        internal static Regex kDeviceInfoRegex = new Regex(@"(?<id>^\S+)\s+(?<state>\S+$)");

        private const int kMillisecondsBetweenConsecutiveDeviceChecks = 1000;

        private IAndroidLogcatDevice m_SelectedDevice;
        private Dictionary<string, IAndroidLogcatDevice> m_Devices = new Dictionary<string, IAndroidLogcatDevice>();
        private IAndroidLogcatRuntime m_Runtime;
        private DateTime m_TimeOfLastDeviceListUpdate;

        public event Action<IAndroidLogcatDevice> DeviceSelected;
        public event Action DevicesUpdated;

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
                ;
                foreach (var d in m_Devices)
                {
                    if (d.Value.State != IAndroidLogcatDevice.DeviceState.Connected)
                        continue;
                    return d.Value;
                }
                return null;
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

            if (device != null && device.State != IAndroidLogcatDevice.DeviceState.Connected)
            {
                AndroidLogcatInternalLog.Log("Trying to select device which is not connected: " + device.Id);
                m_SelectedDevice = null;
            }
            else
            {
                m_SelectedDevice = device;
            }

            if (m_SelectedDevice != null && !m_Devices.Keys.Contains(m_SelectedDevice.Id))
                throw new Exception("Selected device is not among our listed devices");

            if (notifyListeners && DeviceSelected != null)
                DeviceSelected.Invoke(m_SelectedDevice);
        }

        internal static bool ParseDeviceInfo(string input, out string id, out IAndroidLogcatDevice.DeviceState state)
        {
            var result = kDeviceInfoRegex.Match(input);
            if (result.Success)
            {
                id = result.Groups["id"].Value;
                var stateValue = result.Groups["state"].Value.ToLowerInvariant();
                if (stateValue.Equals("device"))
                    state = IAndroidLogcatDevice.DeviceState.Connected;
                else if (stateValue.Equals("offline"))
                    state = IAndroidLogcatDevice.DeviceState.Disconnected;
                else if (stateValue.Equals("unauthorized"))
                    state = IAndroidLogcatDevice.DeviceState.Unauthorized;
                else
                    state = IAndroidLogcatDevice.DeviceState.Unknown;
                return true;
            }
            else
            {
                id = input;
                state = IAndroidLogcatDevice.DeviceState.Unknown;
                return false;
            }
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
                    AndroidLogcatRetrieveDeviceIdsResult.DeviceInfo info;
                    if (ParseDeviceInfo(line, out info.id, out info.state))
                        result.deviceInfo.Add(info);
                }
            }
            catch (Exception ex)
            {
                AndroidLogcatInternalLog.Log(ex.Message);
                result.deviceInfo = new List<AndroidLogcatRetrieveDeviceIdsResult.DeviceInfo>();
            }

            return result;
        }

        private void IntegrateQueryDevices(IAndroidLogcatTaskResult resut)
        {
            var deviceIdsResult = ((AndroidLogcatRetrieveDeviceIdsResult)resut);
            var deviceInfos = deviceIdsResult.deviceInfo;

            foreach (var d in m_Devices)
            {
                ((AndroidLogcatDevice)d.Value).UpdateState(IAndroidLogcatDevice.DeviceState.Disconnected);
            }

            foreach (var info in deviceInfos)
            {
                ((AndroidLogcatDevice)GetOrCreateDevice(info.id)).UpdateState(info.state);
            }

            // If our selected device was removed, deselect it
            if (m_SelectedDevice != null && m_SelectedDevice.State != IAndroidLogcatDevice.DeviceState.Connected)
            {
                m_SelectedDevice = null;
                if (deviceIdsResult.notifyListeners && DeviceSelected != null)
                    DeviceSelected.Invoke(m_SelectedDevice);
            }

            if (m_SelectedDevice != null)
            {
                if (m_SelectedDevice != m_Devices[m_SelectedDevice.Id])
                    throw new Exception("The selected device is not among our list of devices");
            }

            if (DevicesUpdated != null)
                DevicesUpdated.Invoke();
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
