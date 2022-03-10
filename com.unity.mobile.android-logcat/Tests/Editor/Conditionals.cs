using System;
using UnityEditor;
using UnityEngine.TestTools;

[InitializeOnLoad]
public class OnLoad
{
    static OnLoad()
    {
        var value = Environment.GetEnvironmentVariable("ANDROID_DEVICE_IS_AVAILABLE");
        // Always treat that device is available for tests running locally
        var androidDeviceIsAvailable = string.IsNullOrEmpty(value) || value != "0";
        ConditionalIgnoreAttribute.AddConditionalIgnoreMapping(RequiresAndroidDevice.Name, !androidDeviceIsAvailable);
    }
}

public class RequiresAndroidDevice : ConditionalIgnoreAttribute
{
    internal const string Name = nameof(RequiresAndroidDevice);
    public RequiresAndroidDevice() :
        base(Name, "Requires Android Device")
    { }
}
