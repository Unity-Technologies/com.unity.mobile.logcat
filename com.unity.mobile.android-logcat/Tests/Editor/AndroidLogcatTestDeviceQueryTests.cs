using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Android.Logcat;
using UnityEditor.Android;


class AndroidLogcatFakeDeviceQuery : AndroidLogcatDeviceQueryBase
{
    List<string> m_QueuedInfos = new List<string>();


    internal AndroidLogcatFakeDeviceQuery(IAndroidLogcatRuntime runtime)
        : base(runtime)
    {
    }

    internal void QueueDeviceInfos(string infos)
    {
        m_QueuedInfos.Add(infos);
    }

    internal override void UpdateConnectedDevicesList(bool synchronous)
    {
        m_Runtime.Dispatcher.Schedule(new AndroidLogcatRetrieveDeviceIdsInput() { adb = null, notifyListeners = true }, QueryDevicesAsync, IntegrateQueryDevices, synchronous);
    }

    private IAndroidLogcatTaskResult QueryDevicesAsync(IAndroidLogcatTaskInput input)
    {
        var result = new AndroidLogcatRetrieveDeviceIdsResult();
        result.notifyListeners = ((AndroidLogcatRetrieveDeviceIdsInput)input).notifyListeners;

        try
        {
            var adbOutput = m_QueuedInfos.Count > 0 ? m_QueuedInfos[0] : string.Empty;
            if (m_QueuedInfos.Count > 0)
                m_QueuedInfos.RemoveAt(0);

            foreach (var line in adbOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()))
            {
                AndroidLogcatRetrieveDeviceIdsResult.DeviceInfo info;
                if (ParseDeviceInfo(line, out info.id, out info.state))
                    result.deviceInfo.Add(info);
            }
        }
        catch (Exception)
        {
            result.deviceInfo = new List<AndroidLogcatRetrieveDeviceIdsResult.DeviceInfo>();
        }

        return result;
    }

    protected override IAndroidLogcatDevice CreateDevice(string deviceId)
    {
        return new AndroidLogcatFakeDevice90(deviceId);
    }
}

class AndroidLogcatDeviceQueryTests : AndroidLogcatRuntimeTestBase
{
    int m_UpdatedCount = 0;
    int m_SelectedCount = 0;

    [Test]
    public void CheckConnections()
    {
        m_UpdatedCount = 0;
        m_SelectedCount = 0;
        bool notUsed = true;
        InitRuntime();

        var query = (AndroidLogcatFakeDeviceQuery)m_Runtime.DeviceQuery;
        query.DeviceSelected += Query_DeviceSelected;
        query.DevicesUpdated += Query_DevicesUpdated;

        query.QueueDeviceInfos(@"invalid information which shouldn't be parsed
myandroid1 device
myandroid2 device
");
        query.UpdateConnectedDevicesList(notUsed);

        var devices = query.Devices.Values.ToArray();
        Assert.AreEqual(2, devices.Length);
        Assert.AreEqual(devices[0].Id, "myandroid1");
        Assert.AreEqual(devices[0].State, IAndroidLogcatDevice.DeviceState.Connected);
        Assert.AreEqual(devices[1].Id, "myandroid2");
        Assert.AreEqual(devices[1].State, IAndroidLogcatDevice.DeviceState.Connected);
        Assert.AreEqual(m_UpdatedCount, 1);

        query.QueueDeviceInfos("");
        query.UpdateConnectedDevicesList(notUsed);

        Assert.AreEqual(devices[0].State, IAndroidLogcatDevice.DeviceState.Disconnected);
        Assert.AreEqual(devices[1].State, IAndroidLogcatDevice.DeviceState.Disconnected);
        Assert.AreEqual(m_UpdatedCount, 2);


        query.QueueDeviceInfos(@"invalid information which shouldn't be parsed
myandroid1 device
myandroid3 offline
");
        query.UpdateConnectedDevicesList(notUsed);

        devices = query.Devices.Values.ToArray();

        Assert.AreEqual(3, devices.Length);
        Assert.AreEqual(devices[0].Id, "myandroid1");
        Assert.AreEqual(devices[0].State, IAndroidLogcatDevice.DeviceState.Connected);
        Assert.AreEqual(devices[1].Id, "myandroid2");
        Assert.AreEqual(devices[1].State, IAndroidLogcatDevice.DeviceState.Disconnected);
        Assert.AreEqual(devices[2].Id, "myandroid3");
        Assert.AreEqual(devices[2].State, IAndroidLogcatDevice.DeviceState.Disconnected);
        Assert.AreEqual(m_UpdatedCount, 3);

        // Trying to select disconected device does nothing
        query.SelectDevice(devices[1]);
        Assert.AreEqual(0, m_SelectedCount);
        Assert.AreEqual(null, query.SelectedDevice);

        query.SelectDevice(devices[0]);
        Assert.AreEqual(1, m_SelectedCount);
        Assert.AreEqual(devices[0], query.SelectedDevice);

        // No devices, selected device should deselect
        query.QueueDeviceInfos("");
        query.UpdateConnectedDevicesList(notUsed);

        Assert.AreEqual(null, query.SelectedDevice);
        Assert.AreEqual(2, m_SelectedCount);

        ShutdownRuntime();
    }

    private void Query_DevicesUpdated()
    {
        m_UpdatedCount++;
    }

    private void Query_DeviceSelected(IAndroidLogcatDevice obj)
    {
        m_SelectedCount++;
    }
}