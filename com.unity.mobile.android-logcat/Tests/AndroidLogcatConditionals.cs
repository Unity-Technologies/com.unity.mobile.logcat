using System;
using UnityEditor;
using UnityEngine.TestTools;

namespace Unity.Android.Logcat
{
    [InitializeOnLoad]
    public class OnLoad
    {
        static OnLoad()
        {
            var runningOnYamato = Workspace.IsRunningOnYamato();
            var androidDeviceInfoAvailable = Workspace.IsAndroidDeviceInfoAvailable();

            Console.WriteLine($"Running On Yamato: {runningOnYamato}");
            Console.WriteLine($"Android Device Info Available: {androidDeviceInfoAvailable}");

            // Ignore test only if running on Yamato and device info is not available
            ConditionalIgnoreAttribute.AddConditionalIgnoreMapping(nameof(RequiresAndroidDeviceAttribute), runningOnYamato && !androidDeviceInfoAvailable);
        }
    }

    public class RequiresAndroidDeviceAttribute : ConditionalIgnoreAttribute
    {
        public RequiresAndroidDeviceAttribute() :
            base(nameof(RequiresAndroidDeviceAttribute), "Not running since we don't have Android Device")
        { }
    }
}
