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
            var androidDeviceInfo = Workspace.GetAndroidDeviceInfo();

            Console.WriteLine($"Running On Yamato: {runningOnYamato}");
            Console.WriteLine($"Android Device Info: {androidDeviceInfo}");

            // Ignore test only if running on Yamato and device info is not available
            // We always want our test to run when running locally
            ConditionalIgnoreAttribute.AddConditionalIgnoreMapping(nameof(RequiresAndroidDeviceAttribute), runningOnYamato && string.IsNullOrEmpty(androidDeviceInfo));
        }
    }

    public class RequiresAndroidDeviceAttribute : ConditionalIgnoreAttribute
    {
        public RequiresAndroidDeviceAttribute() :
            base(nameof(RequiresAndroidDeviceAttribute), "Not running since we don't have Android Device")
        { }
    }
}
