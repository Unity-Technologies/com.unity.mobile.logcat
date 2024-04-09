using System;
using System.Collections.Generic;
using Unity.Android.Logcat;

internal abstract class AndroidLogcatFakeDevice : IAndroidLogcatDevice
{
    private string m_DeviceId;
    Dictionary<string, string> m_TagPriorities = new Dictionary<string, string>();

    internal override string Manufacturer
    {
        get { return "Undefined"; }
    }

    internal override string Model
    {
        get { return "Undefined"; }
    }

    internal override string ABI
    {
        get { return "Undefined"; }
    }

    internal override string Id
    {
        get { return m_DeviceId; }
    }

    internal override string DisplayName => throw new NotImplementedException();

    internal override string ShortDisplayName => throw new NotImplementedException();

    protected override string GetTagPriorityAsString(string tag)
    {
        if (m_TagPriorities.TryGetValue(tag, out var priority))
            return priority;
        return string.Empty;
    }

    protected override void SetTagPriorityAsString(string tag, string priority)
    {
        m_TagPriorities[tag] = priority;
    }

    internal AndroidLogcatFakeDevice(string deviceId)
        : base(null)
    {
        m_DeviceId = deviceId;
    }

    public override string ToString()
    {
        return $"{GetType().FullName}({m_DeviceId})";
    }
}

internal class AndroidLogcatFakeDevice90 : AndroidLogcatFakeDevice
{
    internal override int APILevel
    {
        get { return 28; }
    }
    internal override Version OSVersion
    {
        get { return new Version(9, 0); }
    }

    internal AndroidLogcatFakeDevice90(string deviceId) : base(deviceId)
    {
    }
}

internal class AndroidLogcatFakeDevice60 : AndroidLogcatFakeDevice
{
    internal override int APILevel
    {
        get { return 23; }
    }
    internal override Version OSVersion
    {
        get { return new Version(6, 0); }
    }

    internal AndroidLogcatFakeDevice60(string deviceId) : base(deviceId)
    {
    }
}
